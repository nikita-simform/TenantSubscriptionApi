using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TenantSubscriptionApi.Data.Public;
using TenantSubscriptionApi.Domain.Entities;

namespace TenantSubscriptionApi.Logging;

// Deliberately opens its OWN connection/DbContext rather than sharing the caller's
// _publicDb — a rejected invite (seat limit) or a denied cross-tenant attempt needs
// its audit entry to survive even though the triggering operation's own transaction
// is about to roll back or throw. This is what you'd query in production to detect
// a tenant leak, not just explain one after the fact.
public class AuditLogger
{
    private readonly string _connectionString;

    public AuditLogger(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task RecordAsync(string action, Guid? organizationId = null, Guid? actorUserId = null, object? metadata = null)
    {
        var options = new DbContextOptionsBuilder<PublicDbContext>().UseNpgsql(_connectionString).Options;
        await using var db = new PublicDbContext(options);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata)
        });

        await db.SaveChangesAsync();
    }
}
