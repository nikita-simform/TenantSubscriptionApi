namespace TenantSubscriptionApi.Contracts.Responses;

public record SignupResponse(Guid OrganizationId, string SchemaName, Guid AdminUserId);
