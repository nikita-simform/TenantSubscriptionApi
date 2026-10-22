using Microsoft.EntityFrameworkCore;
using Npgsql;
using TenantSubscriptionApi.Data.Public;
using TenantSubscriptionApi.Data.Tenant;
using TenantSubscriptionApi.Domain.Entities;
using TenantSubscriptionApi.Logging;
using TenantSubscriptionApi.Services.Org;
using TenantSubscriptionApi.Services.Tenant;
using TenantSubscriptionApi.Tenancy;
using Xunit;

namespace TenantSubscriptionApi.Tests.Services;

// Unlike IsolationSpikeTests (M0, which proves the raw DbContext/search_path
// mechanism in isolation), this exercises the REAL production path: real
// onboarding creates real schemas, then ResourceService — the exact class
// ResourcesController uses — is what's asked to cross the boundary.
public class IsolationTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=localhost;Port=5432;Database=tenant_subscription_poc_test;Username=postgres;Password=postgres";

    private const string OrgAName = "Isolation Test Org A";
    private const string OrgBName = "Isolation Test Org B";

    private OnboardingResult _orgA = null!;
    private OnboardingResult _orgB = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<PublicDbContext>().UseNpgsql(TestConnectionString).Options;
        await using var db = new PublicDbContext(options);
        await db.Database.MigrateAsync();

        await CleanupOrgIfExists(db, OrgAName);
        await CleanupOrgIfExists(db, OrgBName);

        if (!await db.Plans.AnyAsync(p => p.Name == "Free"))
        {
            db.Plans.Add(new Plan { Id = Guid.NewGuid(), Name = "Free", MaxUsers = 3, MaxStorageMB = 100 });
            await db.SaveChangesAsync();
        }

        var scriptProvider = new TenantMigrationScriptProvider(TestConnectionString);
        var provisioner = new TenantSchemaProvisioner(scriptProvider);
        var onboardingService = new OnboardingService(TestConnectionString, provisioner, new AuditLogger(TestConnectionString));

        _orgA = await onboardingService.OnboardOrganizationAsync(OrgAName, "admin-a@isolation-test.local", "password123");
        _orgB = await onboardingService.OnboardOrganizationAsync(OrgBName, "admin-b@isolation-test.local", "password123");
    }

    public async Task DisposeAsync()
    {
        var options = new DbContextOptionsBuilder<PublicDbContext>().UseNpgsql(TestConnectionString).Options;
        await using var db = new PublicDbContext(options);
        await CleanupOrgIfExists(db, OrgAName);
        await CleanupOrgIfExists(db, OrgBName);
    }

    private static async Task CleanupOrgIfExists(PublicDbContext db, string orgName)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Name == orgName);
        if (org is null)
            return;

        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"public\".\"Users\" WHERE \"OrganizationId\" = {org.Id}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"public\".\"Organizations\" WHERE \"Id\" = {org.Id}");

        await using var connection = new NpgsqlConnection(TestConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP SCHEMA IF EXISTS \"{org.SchemaName}\" CASCADE";
        await command.ExecuteNonQueryAsync();
    }

    private static ResourceService CreateResourceService(string schemaName)
    {
        var csb = new NpgsqlConnectionStringBuilder(TestConnectionString) { SearchPath = schemaName };
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(csb.ToString(), npgsql => npgsql.MigrationsHistoryTable(TenantDbContext.MigrationsHistoryTableName, schemaName))
            .Options;
        return new ResourceService(new TenantDbContext(options));
    }

    [Fact]
    public async Task ResourceCreatedInOrgB_IsUnreachableFromOrgA_ByExactId()
    {
        var resourceServiceB = CreateResourceService(_orgB.SchemaName);
        var createdInB = await resourceServiceB.CreateAsync("org-b-secret", "classified content belonging to org B", Guid.NewGuid());

        // A well-formed request for B's resource, made through A's own ResourceService —
        // the exact scenario the spec calls the sharpest test in the whole POC.
        var resourceServiceA = CreateResourceService(_orgA.SchemaName);
        var foundFromA = await resourceServiceA.GetByIdAsync(createdInB.Id);

        Assert.Null(foundFromA);
    }

    [Fact]
    public async Task UnfilteredListFromOrgA_NeverContainsOrgBsResources()
    {
        var resourceServiceA = CreateResourceService(_orgA.SchemaName);
        var resourceServiceB = CreateResourceService(_orgB.SchemaName);

        await resourceServiceA.CreateAsync("org-a-resource", "belongs to A", Guid.NewGuid());
        await resourceServiceB.CreateAsync("org-b-resource", "belongs to B", Guid.NewGuid());

        var allFromA = await resourceServiceA.GetAllAsync();

        Assert.Single(allFromA);
        Assert.Equal("org-a-resource", allFromA[0].Name);
    }
}
