using TenantSubscriptionApi.Domain.Enums;

namespace TenantSubscriptionApi.Contracts.Responses;

public record OrgMemberSummary(Guid Id, string Email, UserRole Role, DateTimeOffset CreatedAt)
{
    public static OrgMemberSummary From(Domain.Entities.User user) =>
        new(user.Id, user.Email, user.Role, user.CreatedAt);
}
