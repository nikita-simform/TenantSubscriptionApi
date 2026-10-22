using Microsoft.EntityFrameworkCore;
using Npgsql;
using TenantSubscriptionApi.Data.Public;
using TenantSubscriptionApi.Data.Tenant;

namespace TenantSubscriptionApi.Tenancy;

// Runs once at startup: loops over every EXISTING org and catches its schema up to
// the latest tenant migrations. Plain Database.MigrateAsync() per org is correct and
// idempotent IF the history table's schema is passed explicitly here — confirmed by
// direct testing that EF Core's Npgsql migrator's existence check does NOT reliably
// resolve an unqualified history table name via the connection's search_path across
// repeated calls (it works the first time a schema is migrated, then incorrectly
// reports "no history" on a later call against that same already-migrated schema,
// attempting to recreate tables that already exist). Passing the schema explicitly
// to MigrationsHistoryTable sidesteps this entirely — each org's own
// __TenantMigrationsHistory (sitting in ITS schema) then correctly tracks what that
// org has applied, with no hand-rolled bookkeeping table needed. New orgs don't need
// this propagator at all — OnboardingService always replays the FULL migration
// history into a fresh schema via raw script (not MigrateAsync), so they start fully
// caught up regardless of this quirk.
public class TenantMigrationPropagator : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string _tenantBaseConnectionString;

    public TenantMigrationPropagator(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _tenantBaseConnectionString = configuration.GetConnectionString("TenantBase")!;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
        var schemaNames = await publicDb.Organizations.Select(o => o.SchemaName).ToListAsync(cancellationToken);

        foreach (var schemaName in schemaNames)
        {
            var csb = new NpgsqlConnectionStringBuilder(_tenantBaseConnectionString) { SearchPath = schemaName };
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql(csb.ToString(), npgsql => npgsql.MigrationsHistoryTable(TenantDbContext.MigrationsHistoryTableName, schemaName))
                .Options;

            await using var tenantDb = new TenantDbContext(options);
            await tenantDb.Database.MigrateAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
