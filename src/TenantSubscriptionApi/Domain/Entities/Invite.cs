namespace TenantSubscriptionApi.Domain.Entities;

// Audit record of a provisioned user — this POC provisions accounts immediately
// (no email delivery, no pending-token flow), so this is a record of what happened,
// not a pending invitation to accept later.
public class Invite
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = "";
    public Domain.Enums.UserRole Role { get; set; }
    public Guid InvitedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
