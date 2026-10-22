using Microsoft.EntityFrameworkCore;
using TenantSubscriptionApi.Data.Public;
using TenantSubscriptionApi.Domain.Entities;
using TenantSubscriptionApi.Domain.Enums;
using TenantSubscriptionApi.Domain.Exceptions;
using TenantSubscriptionApi.Logging;
using TenantSubscriptionApi.Services.Org;
using Xunit;

namespace TenantSubscriptionApi.Tests.Services;

// Proves the authorization boundary OrgUserService.cs documents: a lookup scoped
// by the CALLER's own organization id simply finds nothing for a user belonging to
// a different org — this doesn't need a real tenant schema since OrgUserService
// only ever touches public-schema tables.
public class AuthorizationTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=localhost;Port=5432;Database=tenant_subscription_poc_test;Username=postgres;Password=postgres";

    private Guid _orgAId;
    private Guid _orgBId;
    private Guid _orgBUserId;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<PublicDbContext>().UseNpgsql(TestConnectionString).Options;
        await using var db = new PublicDbContext(options);
        await db.Database.MigrateAsync();

        foreach (var name in new[] { "Authz Test Org A", "Authz Test Org B" })
        {
            var existing = await db.Organizations.FirstOrDefaultAsync(o => o.Name == name);
            if (existing is not null)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"public\".\"Users\" WHERE \"OrganizationId\" = {existing.Id}");
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"public\".\"Organizations\" WHERE \"Id\" = {existing.Id}");
            }
        }

        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Name == "Free");
        if (plan is null)
        {
            plan = new Plan { Id = Guid.NewGuid(), Name = "Free", MaxUsers = 3, MaxStorageMB = 100 };
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
        }

        var orgA = new Organization { Id = Guid.NewGuid(), Name = "Authz Test Org A", SchemaName = "org_authz_test_a_unused", PlanId = plan.Id, UserCount = 1 };
        var orgB = new Organization { Id = Guid.NewGuid(), Name = "Authz Test Org B", SchemaName = "org_authz_test_b_unused", PlanId = plan.Id, UserCount = 1 };
        db.Organizations.AddRange(orgA, orgB);

        var orgBUser = new User { Id = Guid.NewGuid(), Email = "admin-b@authz-test.local", PasswordHash = "n/a", Role = UserRole.OrgAdmin, OrganizationId = orgB.Id };
        db.Users.Add(orgBUser);
        await db.SaveChangesAsync();

        _orgAId = orgA.Id;
        _orgBId = orgB.Id;
        _orgBUserId = orgBUser.Id;
    }

    public async Task DisposeAsync()
    {
        var options = new DbContextOptionsBuilder<PublicDbContext>().UseNpgsql(TestConnectionString).Options;
        await using var db = new PublicDbContext(options);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"public\".\"Users\" WHERE \"OrganizationId\" IN ({_orgAId}, {_orgBId})");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"public\".\"Organizations\" WHERE \"Id\" IN ({_orgAId}, {_orgBId})");
    }

    [Fact]
    public async Task OrgAdmin_CannotRemove_AnotherOrgsUser_ByExactId()
    {
        var options = new DbContextOptionsBuilder<PublicDbContext>().UseNpgsql(TestConnectionString).Options;
        await using var db = new PublicDbContext(options);
        var service = new OrgUserService(db, new AuditLogger(TestConnectionString));

        // Org A's admin (caller org = _orgAId) tries to remove a user whose real id
        // belongs to Org B — a well-formed request naming a real user that simply
        // isn't theirs to touch.
        await Assert.ThrowsAsync<NotFoundException>(() => service.RemoveUserAsync(_orgAId, _orgBUserId));

        var stillExists = await db.Users.AnyAsync(u => u.Id == _orgBUserId);
        Assert.True(stillExists);
    }

    [Fact]
    public async Task OrgAdmin_CannotChangeRole_OfAnotherOrgsUser_ByExactId()
    {
        var options = new DbContextOptionsBuilder<PublicDbContext>().UseNpgsql(TestConnectionString).Options;
        await using var db = new PublicDbContext(options);
        var service = new OrgUserService(db, new AuditLogger(TestConnectionString));

        await Assert.ThrowsAsync<NotFoundException>(() => service.ChangeRoleAsync(_orgAId, _orgBUserId, UserRole.OrgMember));

        var unchanged = await db.Users.FirstAsync(u => u.Id == _orgBUserId);
        Assert.Equal(UserRole.OrgAdmin, unchanged.Role);
    }
}
