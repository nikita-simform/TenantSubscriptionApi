using System.ComponentModel.DataAnnotations;
using TenantSubscriptionApi.Domain.Enums;

namespace TenantSubscriptionApi.Contracts.Requests;

public record InviteUserRequest(
    [Required, EmailAddress] string Email,
    UserRole Role = UserRole.OrgMember
);
