using System.ComponentModel.DataAnnotations;

namespace TenantSubscriptionApi.Contracts.Requests;

public record CreateResourceRequest(
    [Required, MaxLength(200)] string Name,
    [Required] string Content
);
