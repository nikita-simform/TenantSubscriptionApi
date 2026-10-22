using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantSubscriptionApi.Contracts.Requests;
using TenantSubscriptionApi.Contracts.Responses;
using TenantSubscriptionApi.Domain.Enums;
using TenantSubscriptionApi.Services.Org;
using TenantSubscriptionApi.Tenancy;

namespace TenantSubscriptionApi.Controllers;

// {orgId} appears in the route for a normal-looking REST URL, but it is NEVER used
// for scoping below — every operation is scoped by _tenantContext.OrganizationId,
// resolved from the caller's OWN JWT. This is deliberate: an org admin naming a
// DIFFERENT org's id here (or a different org's user id) must not change what they
// can reach. [Authorize(Roles = "OrgAdmin")] only confirms the caller's ROLE; the
// services below (OrgUserService/InviteService) are what enforce the org boundary.
[Authorize(Roles = nameof(UserRole.OrgAdmin))]
[ApiController]
[Route("api/orgs/{orgId}/users")]
public class OrgUsersController : ControllerBase
{
    private readonly InviteService _inviteService;
    private readonly OrgUserService _orgUserService;
    private readonly ITenantContext _tenantContext;

    public OrgUsersController(InviteService inviteService, OrgUserService orgUserService, ITenantContext tenantContext)
    {
        _inviteService = inviteService;
        _orgUserService = orgUserService;
        _tenantContext = tenantContext;
    }

    [HttpPost]
    public async Task<ActionResult<InviteResponse>> Invite(Guid orgId, InviteUserRequest request)
    {
        var result = await _inviteService.InviteUserAsync(
            _tenantContext.OrganizationId!.Value, _tenantContext.UserId!.Value, request.Email, request.Role);

        return Ok(new InviteResponse(result.UserId, result.TemporaryPassword));
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> Remove(Guid orgId, Guid userId)
    {
        await _orgUserService.RemoveUserAsync(_tenantContext.OrganizationId!.Value, userId);
        return NoContent();
    }

    [HttpPut("{userId}/role")]
    public async Task<IActionResult> ChangeRole(Guid orgId, Guid userId, ChangeRoleRequest request)
    {
        await _orgUserService.ChangeRoleAsync(_tenantContext.OrganizationId!.Value, userId, request.Role);
        return NoContent();
    }
}
