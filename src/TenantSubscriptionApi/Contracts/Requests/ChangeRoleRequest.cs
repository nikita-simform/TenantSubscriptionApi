using TenantSubscriptionApi.Domain.Enums;

namespace TenantSubscriptionApi.Contracts.Requests;

public record ChangeRoleRequest(UserRole Role);
