namespace TenantSubscriptionApi.Contracts.Responses;

// Deliberately only plan/usage metadata — no field here could ever expose a byte of
// an organization's actual content, by construction (see PlatformAdminService).
public record OrganizationSummary(
    Guid Id,
    string Name,
    string PlanName,
    int UserCount,
    int MaxUsers,
    int StorageUsedMB,
    int MaxStorageMB
);
