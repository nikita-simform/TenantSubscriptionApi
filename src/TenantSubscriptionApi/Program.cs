using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using TenantSubscriptionApi.Data.Public;
using TenantSubscriptionApi.Data.Tenant;
using TenantSubscriptionApi.Domain.Entities;
using TenantSubscriptionApi.ExceptionHandling;
using TenantSubscriptionApi.Logging;
using TenantSubscriptionApi.Middleware;
using TenantSubscriptionApi.Services.Auth;
using TenantSubscriptionApi.Services.Org;
using TenantSubscriptionApi.Services.Platform;
using TenantSubscriptionApi.Services.Tenant;
using TenantSubscriptionApi.Tenancy;

// By default, JwtSecurityTokenHandler silently REMAPS short inbound JWT claim types
// to longer legacy WS-Federation URIs when validating a token (e.g. "sub" becomes
// ClaimTypes.NameIdentifier, a completely different string) — so looking up
// JwtRegisteredClaimNames.Sub after validation finds nothing, even though the token
// visibly contains a "sub" claim. Disabling this keeps claim type strings identical
// on both ends: whatever JwtTokenService writes is exactly what TenantResolutionMiddleware reads.
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Serialize enums (UserRole, etc.) as their string names ("OrgAdmin") instead of
// System.Text.Json's default raw integers — far more readable for API consumers,
// and makes request bodies like {"role":"OrgMember"} work instead of requiring {"role":2}.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Dev-only: lets the React demo app (Vite dev server, a different origin) call this
// API directly from the browser. Scoped to known local dev origins, not a wildcard.
const string DevCorsPolicy = "DevCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy => policy
        .WithOrigins(
            "http://localhost:5173", "http://127.0.0.1:5173",
            "http://localhost:3000", "http://127.0.0.1:3000",
            "http://localhost:5174", "http://127.0.0.1:5174")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Control-plane data (Organizations, Plans, Users, ...) — fixed connection, always "public" schema.
builder.Services.AddDbContext<PublicDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Public")));

// Populated per-request by TenantResolutionMiddleware, reading the validated JWT's
// claims. Registered both ways so the concrete type the middleware mutates and the
// interface services consume resolve to the SAME scoped instance.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

builder.Services.AddScoped<ResourceService>();

builder.Services.AddSingleton<AuditLogger>(
    _ => new AuditLogger(builder.Configuration.GetConnectionString("Public")!));

builder.Services.AddSingleton<ITenantMigrationScriptProvider>(
    _ => new TenantMigrationScriptProvider(builder.Configuration.GetConnectionString("TenantBase")!));
builder.Services.AddScoped<TenantSchemaProvisioner>();
builder.Services.AddScoped<OnboardingService>(sp => new OnboardingService(
    builder.Configuration.GetConnectionString("Public")!,
    sp.GetRequiredService<TenantSchemaProvisioner>(),
    sp.GetRequiredService<AuditLogger>()));

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<InviteService>();
builder.Services.AddScoped<OrgUserService>();
builder.Services.AddScoped<PlatformAdminService>();
builder.Services.AddScoped<PlatformBootstrapService>();

builder.Services.AddHostedService<TenantMigrationPropagator>();

builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Public")!, name: "postgres");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };
    });
builder.Services.AddAuthorization();

// Tenant content (Resource) — same connection string as Public, but with "Search Path"
// pointed at the caller's own organization schema, resolved fresh for every request scope.
// See TenantDbContext.cs for why this context has no schema annotations of its own.
builder.Services.AddDbContext<TenantDbContext>((sp, options) =>
{
    var schemaName = sp.GetRequiredService<ITenantContext>().SchemaName;
    if (schemaName is null)
    {
        // `dotnet ef` discovers every registered DbContext (even when targeting a
        // different --context) by constructing them, which would otherwise trip this
        // guard. EF.IsDesignTime is true only during tooling runs, never in the real app.
        if (!EF.IsDesignTime)
            throw new InvalidOperationException(
                "TenantDbContext was resolved with no tenant schema in scope. " +
                "Only tenant-scoped code paths (never platform-admin code) should depend on TenantDbContext.");
        schemaName = "public";
    }

    var csb = new NpgsqlConnectionStringBuilder(builder.Configuration.GetConnectionString("TenantBase"))
    {
        SearchPath = schemaName
    };
    options.UseNpgsql(csb.ToString(), npgsql => npgsql.MigrationsHistoryTable(TenantDbContext.MigrationsHistoryTableName, schemaName));
});

var app = builder.Build();

// Ensure the public schema is up to date and a default plan exists — mirrors the
// "apply migrations, then seed" sequence M6's docker-entrypoint will formalize.
using (var scope = app.Services.CreateScope())
{
    var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
    await publicDb.Database.MigrateAsync();

    if (!await publicDb.Plans.AnyAsync(p => p.Name == "Free"))
    {
        publicDb.Plans.Add(new Plan { Id = Guid.NewGuid(), Name = "Free", MaxUsers = 3, MaxStorageMB = 100 });
        await publicDb.SaveChangesAsync();
    }

    var bootstrapService = scope.ServiceProvider.GetRequiredService<PlatformBootstrapService>();
    await bootstrapService.EnsurePlatformAdminAsync(
        app.Configuration["SeedPlatformAdmin:Email"]!,
        app.Configuration["SeedPlatformAdmin:Password"]!);
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
    app.UseCors(DevCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

// Needs context.User already populated by UseAuthentication() above, and must run
// before controllers so ITenantContext is ready by the time one is injected.
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
