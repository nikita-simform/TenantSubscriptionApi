using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TenantSubscriptionApi.Domain.Enums;
using TenantSubscriptionApi.Tenancy;

namespace TenantSubscriptionApi.Middleware;

// Replaces the M1 stub. Must run AFTER UseAuthentication() (needs context.User
// already populated from the validated JWT) and BEFORE controllers. Every claim
// read here was put there by JwtTokenService at login — this is the other half
// of that contract.
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            tenantContext.UserId = Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
            tenantContext.Role = Enum.Parse<UserRole>(user.FindFirstValue(ClaimTypes.Role)!);

            var organizationId = user.FindFirstValue("organizationId");
            tenantContext.OrganizationId = organizationId is null ? null : Guid.Parse(organizationId);
            tenantContext.SchemaName = user.FindFirstValue("schemaName");
        }

        await _next(context);
    }
}
