using Microsoft.AspNetCore.Mvc;
using TenantSubscriptionApi.Contracts.Requests;
using TenantSubscriptionApi.Contracts.Responses;
using TenantSubscriptionApi.Services.Org;

namespace TenantSubscriptionApi.Controllers;

// Self-service onboarding — the only unauthenticated endpoint in the system besides
// login (M3). No tenant context needed here: the org doesn't exist yet to scope to.
[ApiController]
[Route("api/signup")]
public class SignupController : ControllerBase
{
    private readonly OnboardingService _onboardingService;

    public SignupController(OnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    [HttpPost]
    public async Task<ActionResult<SignupResponse>> Post(SignupRequest request)
    {
        var result = await _onboardingService.OnboardOrganizationAsync(
            request.OrganizationName, request.AdminEmail, request.AdminPassword);

        return Ok(new SignupResponse(result.OrganizationId, result.SchemaName, result.AdminUserId));
    }
}
