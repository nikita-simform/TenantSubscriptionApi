using System.ComponentModel.DataAnnotations;

namespace TenantSubscriptionApi.Contracts.Requests;

public record SignupRequest(
    [Required, MaxLength(200)] string OrganizationName,
    [Required, EmailAddress] string AdminEmail,
    [Required, MinLength(8)] string AdminPassword
);
