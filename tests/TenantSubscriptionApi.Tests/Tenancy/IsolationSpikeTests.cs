using Microsoft.EntityFrameworkCore;
using Npgsql;
using TenantSubscriptionApi.Data.Tenant;
using TenantSubscriptionApi.Domain.Entities;
using Xunit;

namespace TenantSubscriptionApi.Tests.Tenancy;

// M0 — proves the core isolation primitive BEFORE anything is built on top of it:
// two TenantDbContext instances, pointed at two real (freshly created) Postgres
// schemas via the connection string's Search Path, never see each other's rows —
// even on an unfiltered query, and even by direct ID. There is no OrganizationId
// column on Resource for a careless query to forget; isolation is structural.
public class IsolationSpikeTests : IAsyncLifetime
{
    private const string BaseConnectionString =
        "Host=localhost;Port=5432;Database=tenant_subscription_poc_test;Username=postgres;Password=postgres";

    private const string SchemaA = "spike_org_a";
    private const string SchemaB = "spike_org_b";

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(BaseConnectionString);
        await connection.OpenAsync();

        foreach (var schema in new[] { SchemaA, SchemaB })
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();

            await using var create = connection.CreateCommand();
            create.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await create.ExecuteNonQueryAsync();
        }

        // Apply the Resources table migration into each schema independently —
        // exactly the mechanism TenantMigrationPropagator will use in M5.
        await using (var contextA = CreateContext(SchemaA))
            await contextA.Database.MigrateAsync();
        await using (var contextB = CreateContext(SchemaB))
            await contextB.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(BaseConnectionString);
        await connection.OpenAsync();
        foreach (var schema in new[] { SchemaA, SchemaB })
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static TenantDbContext CreateContext(string schemaName)
    {
        var csb = new NpgsqlConnectionStringBuilder(BaseConnectionString) { SearchPath = schemaName };
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(csb.ToString(), npgsql => npgsql.MigrationsHistoryTable(TenantDbContext.MigrationsHistoryTableName, schemaName))
            .Options;
        return new TenantDbContext(options);
    }

    [Fact]
    public async Task EachSchema_OnlyEverSeesItsOwnResources_EvenOnAnUnfilteredQuery()
    {
        var resourceA = new Resource { Id = Guid.NewGuid(), Name = "org-a-secret", Content = "a", CreatedByUserId = Guid.NewGuid() };
        var resourceB = new Resource { Id = Guid.NewGuid(), Name = "org-b-secret", Content = "b", CreatedByUserId = Guid.NewGuid() };

        await using (var contextA = CreateContext(SchemaA))
        {
            contextA.Resources.Add(resourceA);
            await contextA.SaveChangesAsync();
        }
        await using (var contextB = CreateContext(SchemaB))
        {
            contextB.Resources.Add(resourceB);
            await contextB.SaveChangesAsync();
        }

        // Fresh context instances — simulating two separate, later requests.
        await using var readA = CreateContext(SchemaA);
        var allFromA = await readA.Resources.ToListAsync();
        Assert.Single(allFromA);
        Assert.Equal("org-a-secret", allFromA[0].Name);

        await using var readB = CreateContext(SchemaB);
        var allFromB = await readB.Resources.ToListAsync();
        Assert.Single(allFromB);
        Assert.Equal("org-b-secret", allFromB[0].Name);
    }

    [Fact]
    public async Task CrossSchemaReadByExactId_FindsNothing()
    {
        var resourceB = new Resource { Id = Guid.NewGuid(), Name = "org-b-only", Content = "b", CreatedByUserId = Guid.NewGuid() };
        await using (var contextB = CreateContext(SchemaB))
        {
            contextB.Resources.Add(resourceB);
            await contextB.SaveChangesAsync();
        }

        // A well-formed request for B's resource ID, made against A's schema — the
        // spec's sharpest requirement: this must find nothing, not someone else's data.
        await using var contextA = CreateContext(SchemaA);
        var found = await contextA.Resources.FindAsync(resourceB.Id);

        Assert.Null(found);
    }
}
