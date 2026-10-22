namespace TenantSubscriptionApi.Domain.Entities;

// Lives in a PER-ORGANIZATION Postgres schema (not the shared public schema).
// Deliberately has NO OrganizationId column — there is nothing to filter by,
// because isolation comes from which schema the connection is bound to, not
// from an application-level WHERE clause on this table.
public class Resource
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Content { get; set; } = "";
    public long SizeBytes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
