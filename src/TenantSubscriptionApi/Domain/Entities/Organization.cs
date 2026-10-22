namespace TenantSubscriptionApi.Domain.Entities;

// Lives in the shared public schema — the control-plane record for a tenant.
// The tenant's actual content lives in a separate Postgres schema named by SchemaName.
public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";       // unique — guards against concurrent duplicate signups
    public string SchemaName { get; set; } = "";  // unique — e.g. "org_acme_corp", points at a real Postgres schema

    public Guid PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    // Live counters, maintained ONLY via the atomic conditional UPDATE in InviteService/ResourceService —
    // never via "load entity, mutate property, SaveChanges", which would race under concurrency.
    public int UserCount { get; set; }
    public int StorageUsedMB { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
