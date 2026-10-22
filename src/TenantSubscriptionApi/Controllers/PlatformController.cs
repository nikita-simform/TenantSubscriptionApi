using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantSubscriptionApi.Contracts.Responses;
using TenantSubscriptionApi.Domain.Enums;
using TenantSubscriptionApi.Services.Platform;

namespace TenantSubscriptionApi.Controllers;

[Authorize(Roles = nameof(UserRole.PlatformAdmin))]
[ApiController]
[Route("api/platform")]
public class PlatformController : ControllerBase
{
    private readonly PlatformAdminService _platformAdminService;

    public PlatformController(PlatformAdminService platformAdminService)
    {
        _platformAdminService = platformAdminService;
    }

    [HttpGet("orgs")]
    public async Task<ActionResult<PagedResult<OrganizationSummary>>> GetOrganizations(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _platformAdminService.ListOrganizationsAsync(page, pageSize);
        return Ok(result);
    }
}
