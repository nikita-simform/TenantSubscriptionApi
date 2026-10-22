using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TenantSubscriptionApi.Data.Public;
using TenantSubscriptionApi.Domain.Entities;
using TenantSubscriptionApi.Domain.Enums;
using TenantSubscriptionApi.Domain.Exceptions;
using TenantSubscriptionApi.Logging;

namespace TenantSubscriptionApi.Services.Org;

public record InviteResult(Guid UserId, string TemporaryPassword);

public class InviteService
{
    private readonly PublicDbContext _publicDb;
    private readonly AuditLogger _auditLogger;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public InviteService(PublicDbContext publicDb, AuditLogger auditLogger)
    {
        _publicDb = publicDb;
        _auditLogger = auditLogger;
    }

    public async Task<InviteResult> InviteUserAsync(Guid organizationId, Guid invitedByUserId, string email, UserRole role)
    {
        await using var transaction = await _publicDb.Database.BeginTransactionAsync();

        // The atomic conditional UPDATE: the WHERE clause re-evaluates against the
        // row's committed value while Postgres holds its write lock, so two concurrent
        // invites on the SAME org serialize on this row even under plain READ COMMITTED
        // isolation — no advisory lock, no SERIALIZABLE transaction needed. The loser
        // gets 0 rows affected. Raw SQL deliberately, not the normal EF Core
        // load-mutate-SaveChanges flow, which would race (read-then-write is not atomic).
        var rowsAffected = await _publicDb.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ""Organizations"" AS o
            SET ""UserCount"" = o.""UserCount"" + 1
            FROM ""Plans"" AS p
            WHERE o.""Id"" = {organizationId}
              AND p.""Id"" = o.""PlanId""
              AND o.""UserCount"" < p.""MaxUsers""");

        if (rowsAffected == 0)
        {
            await _auditLogger.RecordAsync("invite.limit_rejected", organizationId, invitedByUserId, new { email });
            throw new LimitExceededException(
                "This organization has reached its plan's seat limit. Upgrade the plan or remove a user before inviting another.");
        }

        // No real email delivery in this POC — the account is provisioned immediately
        // with a server-generated temporary password, returned once in the response.
        var temporaryPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));

        var newUser = new User { Id = Guid.NewGuid(), Email = email, Role = role, OrganizationId = organizationId };
        newUser.PasswordHash = _passwordHasher.HashPassword(newUser, temporaryPassword);

        _publicDb.Users.Add(newUser);
        _publicDb.Invites.Add(new Invite
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Email = email,
            Role = role,
            InvitedByUserId = invitedByUserId
        });

        try
        {
            await _publicDb.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ValidationException("A user with this email already exists.");
        }

        await transaction.CommitAsync();
        return new InviteResult(newUser.Id, temporaryPassword);
    }
}
