using TenantSubscriptionApi.Domain.Enums;

namespace TenantSubscriptionApi.Domain.Entities;

// Lives in the shared public schema. OrganizationId is null ONLY for PlatformAdmin —
// every OrgAdmin/OrgMember belongs to exactly one organization.
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";  // unique
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }

    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
