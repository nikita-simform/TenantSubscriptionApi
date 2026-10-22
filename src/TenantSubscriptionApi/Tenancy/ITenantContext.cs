using TenantSubscriptionApi.Domain.Enums;

namespace TenantSubscriptionApi.Tenancy;

// Resolved once per request (Scoped) from the caller's JWT claims (via
// TenantResolutionMiddleware, added in M3). Every tenant-scoped DbContext and
// every authorization check reads from this — never from a route parameter —
// so a request can never widen its own access by naming a different org in the URL.
public interface ITenantContext
{
    Guid? UserId { get; }
    UserRole? Role { get; }
    Guid? OrganizationId { get; }
    string? SchemaName { get; }   // null for PlatformAdmin and for an unauthenticated request
    bool IsAuthenticated { get; }
}
