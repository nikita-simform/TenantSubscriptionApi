using Microsoft.EntityFrameworkCore;
using TenantSubscriptionApi.Data.Public;
using TenantSubscriptionApi.Domain.Entities;
using TenantSubscriptionApi.Domain.Enums;
using TenantSubscriptionApi.Domain.Exceptions;
using TenantSubscriptionApi.Logging;
using TenantSubscriptionApi.Services.Org;
using Xunit;

namespace TenantSubscriptionApi.Tests.Services;

// The spec's hardest guarantee: two simultaneous requests that would each fit
// individually, but together exceed the limit, must not both succeed. This only
// proves anything if the two calls race at the DATABASE — using one shared
// DbContext/InviteService instance for both calls would just serialize them in C#
// and never exercise the real race at all, so each call below gets its OWN
// PublicDbContext (its own physical connection).
public class ConcurrencyTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=localhost;Port=5432;Database=tenant_subscription_poc_test;Username=postgres;Password=postgres";

    private const string OrgName = "Concurrency Test Org";
    private Guid _organizationId;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<PublicDbContext>().UseNpgsql(TestConnectionString).Options;
        await using var db = new PublicDbContext(options);
        await db.Database.MigrateAsync();

        var existing = await db.Organizations.FirstOrDefaultAsync(o => o.Name == OrgName);
        if (existing is not null)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"public\".\"Users\" WHERE \"OrganizationId\" = {existing.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"public\".\"Organizations\" WHERE \"Id\" = {existing.Id}");
        }

        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Name == "Free");
        if (plan is null)
        {
            plan = new Plan { Id = Guid.NewGuid(), Name = "Free", MaxUsers = 3, MaxStorageMB = 100 };
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
        }

        // Start at 2/3 seats — one slot left, which is what makes two SIMULTANEOUS
        // invites a genuine race instead of both trivially succeeding.
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = OrgName,
            SchemaName = "org_concurrency_test_unused", // InviteService never touches tenant data; no real schema needed
            PlanId = plan.Id,
            UserCount = 2
        };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();

        _organizationId = organization.Id;
    }

    public async Task DisposeAsync()
    {
        var options = new DbContextOptionsBuilder<PublicDbContext>().UseNpgsql(TestConnectionString).Options;
        await using var db = new PublicDbContext(options);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"public\".\"Users\" WHERE \"OrganizationId\" = {_organizationId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"public\".\"Organizations\" WHERE \"Id\" = {_organizationId}");
    }

    private async Task<bool> TryInvite(string email)
    {
        var options = new DbContextOptionsBuilder<PublicDbContext>().UseNpgsql(TestConnectionString).Options;
        await using var db = new PublicDbContext(options); // own connection — this is what makes the race real
        var inviteService = new InviteService(db, new AuditLogger(TestConnectionString));

        try
        {
            await inviteService.InviteUserAsync(_organizationId, Guid.NewGuid(), email, UserRole.OrgMember);
            return true;
        }
        catch (LimitExceededException)
        {
            return false;
        }
    }

    [Fact]
    public async Task TwoSimultaneousInvites_AtOneSeatRemaining_ExactlyOneSucceeds()
    {
        var results = await Task.WhenAll(
            TryInvite("concurrency-test-a@example.com"),
            TryInvite("concurrency-test-b@example.com"));

        Assert.Equal(1, results.Count(succeeded => succeeded));
        Assert.Equal(1, results.Count(succeeded => !succeeded));

        var checkOptions = new DbContextOptionsBuilder<PublicDbContext>().UseNpgsql(TestConnectionString).Options;
        await using var checkDb = new PublicDbContext(checkOptions);
        var finalUserCount = await checkDb.Organizations
            .Where(o => o.Id == _organizationId)
            .Select(o => o.UserCount)
            .FirstAsync();

        Assert.Equal(3, finalUserCount);
    }
}
