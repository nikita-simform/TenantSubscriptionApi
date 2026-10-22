using System.ComponentModel.DataAnnotations;

namespace TenantSubscriptionApi.Contracts.Requests;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);
