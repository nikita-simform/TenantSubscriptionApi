namespace TenantSubscriptionApi.Contracts.Responses;

public record InviteResponse(Guid UserId, string TemporaryPassword);
