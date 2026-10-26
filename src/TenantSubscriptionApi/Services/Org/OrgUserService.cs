using Microsoft.EntityFrameworkCore;
using TenantSubscriptionApi.Data.Public;
using TenantSubscriptionApi.Domain.Enums;
using TenantSubscriptionApi.Domain.Exceptions;
using TenantSubscriptionApi.Logging;

namespace TenantSubscriptionApi.Services.Org;

// The authorization boundary lives HERE, not in the controller: every lookup is
// scoped by callerOrganizationId (always sourced from ITenantContext, i.e. the
// caller's own JWT — see OrgUsersController). A target user in a DIFFERENT org
// simply doesn't match the WHERE clause, so this throws NotFoundException rather
// than ForbiddenException — it doesn't even reveal that the user exists elsewhere.
public class OrgUserService
{
    private readonly PublicDbContext _publicDb;
    private readonly AuditLogger _auditLogger;

    public OrgUserService(PublicDbContext publicDb, AuditLogger auditLogger)
    {
        _publicDb = publicDb;
        _auditLogger = auditLogger;
    }

    public async Task<List<Domain.Entities.User>> ListMembersAsync(Guid callerOrganizationId)
    {
        return await _publicDb.Users
            .Where(u => u.OrganizationId == callerOrganizationId)
            .OrderBy(u => u.Email)
            .ToListAsync();
    }

    public async Task RemoveUserAsync(Guid callerOrganizationId, Guid targetUserId)
    {
        var user = await FindScopedUserOrThrowAsync(callerOrganizationId, targetUserId);

        await using var transaction = await _publicDb.Database.BeginTransactionAsync();

        _publicDb.Users.Remove(user);
        await _publicDb.SaveChangesAsync();

        // Frees the seat back up for future invites. No race to guard against here
        // (unlike the invite-side increment) — decrementing never needs to check a
        // cap, so a plain statement is sufficient.
        await _publicDb.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Organizations"" SET ""UserCount"" = ""UserCount"" - 1 WHERE ""Id"" = {callerOrganizationId}");

        await transaction.CommitAsync();
    }

    public async Task ChangeRoleAsync(Guid callerOrganizationId, Guid targetUserId, UserRole newRole)
    {
        var user = await FindScopedUserOrThrowAsync(callerOrganizationId, targetUserId);

        user.Role = newRole;
        await _publicDb.SaveChangesAsync();
    }

    // The Users table is shared across all orgs (unlike Resource, which is
    // schema-isolated), so — unlike a cross-tenant resource read, which is
    // structurally undetectable as anything but "not found" — this case CAN
    // distinguish "no such user anywhere" from "that user belongs to someone
    // else," purely for audit purposes. The existence check below never grants
    // access; both branches still throw the identical NotFoundException.
    private async Task<Domain.Entities.User> FindScopedUserOrThrowAsync(Guid callerOrganizationId, Guid targetUserId)
    {
        var user = await _publicDb.Users
            .FirstOrDefaultAsync(u => u.Id == targetUserId && u.OrganizationId == callerOrganizationId);

        if (user is not null)
            return user;

        var existsInAnotherOrg = await _publicDb.Users.AnyAsync(u => u.Id == targetUserId);
        if (existsInAnotherOrg)
        {
            await _auditLogger.RecordAsync("user.cross_tenant_access_denied", callerOrganizationId, null,
                new { targetUserId });
        }

        throw new NotFoundException("User not found.");
    }
}
