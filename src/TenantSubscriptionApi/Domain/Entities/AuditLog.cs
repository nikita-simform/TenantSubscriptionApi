namespace TenantSubscriptionApi.Domain.Entities;

// Structured trail for onboarding, plan-limit rejections, and cross-tenant access
// attempts — this is what would catch a tenant leak in production, not just explain
// one after the fact.
public class AuditLog
{
    public Guid Id { get; set; }
    public string Action { get; set; } = "";       // e.g. "org.onboarded", "invite.limit_rejected", "resource.cross_tenant_denied"
    public Guid? OrganizationId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? MetadataJson { get; set; }        // free-form structured detail, serialized JSON
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
