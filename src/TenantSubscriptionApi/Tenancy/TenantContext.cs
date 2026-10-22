using TenantSubscriptionApi.Domain.Enums;

namespace TenantSubscriptionApi.Tenancy;

// Mutable, Scoped implementation — populated once per request by whatever runs
// first in the pipeline (a stub for M0/M1 testing, real TenantResolutionMiddleware
// reading JWT claims from M3 onward). Nothing downstream needs to know which.
public class TenantContext : ITenantContext
{
    public Guid? UserId { get; set; }
    public UserRole? Role { get; set; }
    public Guid? OrganizationId { get; set; }
    public string? SchemaName { get; set; }
    public bool IsAuthenticated => UserId is not null;
}
