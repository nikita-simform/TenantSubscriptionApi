using Microsoft.EntityFrameworkCore;
using Npgsql;
using TenantSubscriptionApi.Data.Public;
using TenantSubscriptionApi.Domain.Entities;
using TenantSubscriptionApi.Logging;
using TenantSubscriptionApi.Services.Org;
using TenantSubscriptionApi.Tenancy;
using Xunit;

namespace TenantSubscriptionApi.Tests.Services;

// M2's acceptance test: a failure partway through the tenant migration replay must
// leave NOTHING behind — no Organization row, no User row, no orphaned Postgres schema.
// This is what makes onboarding safe to retry after a failure instead of needing
// manual cleanup, and it's the test that proves the single-transaction design in
// OnboardingService actually works (a naive MigrateAsync()-based onboarding would
// NOT pass this once there's more than one tenant migration to replay).
public class OnboardingRollbackTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=localhost;Port=5432;Database=tenant_subscription_poc_test;Username=postgres;Password=postgres";

    private const string OrgName = "Rollback Test Org";
    private const string AdminEmail = "admin@rollback-test.local";

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<PublicDbContext>().UseNpgsql(TestConnectionString).Options;
        await using var db = new PublicDbContext(options);
        await db.Database.MigrateAsync();

        // Clean slate in case a previous failed run left this test's own fixture data behind.
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"public\".\"Users\" WHERE \"Email\" = {AdminEmail}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"public\".\"Organizations\" WHERE \"Name\" = {OrgName}");

        if (!await db.Plans.AnyAsync(p => p.Name == "Free"))
        {
            db.Plans.Add(new Plan { Id = Guid.NewGuid(), Name = "Free", MaxUsers = 3, MaxStorageMB = 100 });
            await db.SaveChangesAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private class BrokenMigrationScriptProvider : ITenantMigrationScriptProvider
    {
        // Deliberately invalid SQL — simulates a bad migration shipping in the tenant
        // schema history, which must not be able to half-apply.
        public string GetScript() => "CREATE TABLE \"Resources\" (this is not valid sql!!!);";
    }

    [Fact]
    public async Task FailedMigrationReplay_LeavesNoOrphanedOrgUserOrSchema()
    {
        var provisioner = new TenantSchemaProvisioner(new BrokenMigrationScriptProvider());
        var onboardingService = new OnboardingService(TestConnectionString, provisioner, new AuditLogger(TestConnectionString));
        var expectedSchema = SchemaNameGenerator.Derive(OrgName);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            onboardingService.OnboardOrganizationAsync(OrgName, AdminEmail, "password123"));

        var options = new DbContextOptionsBuilder<PublicDbContext>().UseNpgsql(TestConnectionString).Options;
        await using var db = new PublicDbContext(options);

        Assert.False(await db.Organizations.AnyAsync(o => o.Name == OrgName));
        Assert.False(await db.Users.AnyAsync(u => u.Email == AdminEmail));

        await using var connection = new NpgsqlConnection(TestConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = @schema";
        command.Parameters.AddWithValue("schema", expectedSchema);
        var schemaCount = (long)(await command.ExecuteScalarAsync())!;

        Assert.Equal(0, schemaCount);
    }
}
