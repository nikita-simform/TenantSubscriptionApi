namespace TenantSubscriptionApi.Contracts.Responses;

public record ResourceResponse(Guid Id, string Name, string Content, long SizeBytes, DateTimeOffset CreatedAt)
{
    public static ResourceResponse From(Domain.Entities.Resource resource) =>
        new(resource.Id, resource.Name, resource.Content, resource.SizeBytes, resource.CreatedAt);
}
