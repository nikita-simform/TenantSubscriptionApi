using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantSubscriptionApi.Contracts.Requests;
using TenantSubscriptionApi.Contracts.Responses;
using TenantSubscriptionApi.Domain.Enums;
using TenantSubscriptionApi.Services.Tenant;
using TenantSubscriptionApi.Tenancy;

namespace TenantSubscriptionApi.Controllers;

// Built in M1 against a stub tenant resolver; M3 swapped in real JWT-based
// resolution (TenantResolutionMiddleware) WITHOUT a single line here changing —
// exactly what depending only on ITenantContext/ResourceService was meant to buy.
// Explicitly excludes PlatformAdmin: without this, a platform admin's token would
// pass [Authorize] and then crash with a 500 when TenantDbContext tries to resolve
// a schema it was never given — correct in effect (access is still impossible) but
// a role check belongs here, not an exception's accidental side effect.
[Authorize(Roles = $"{nameof(UserRole.OrgAdmin)},{nameof(UserRole.OrgMember)}")]
[ApiController]
[Route("api/resources")]
public class ResourcesController : ControllerBase
{
    private readonly ResourceService _resourceService;
    private readonly ITenantContext _tenantContext;

    public ResourcesController(ResourceService resourceService, ITenantContext tenantContext)
    {
        _resourceService = resourceService;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<ResourceResponse>>> GetAll()
    {
        var resources = await _resourceService.GetAllAsync();
        return Ok(resources.Select(ResourceResponse.From));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ResourceResponse>> GetById(Guid id)
    {
        var resource = await _resourceService.GetByIdAsync(id);
        return resource is null ? NotFound() : Ok(ResourceResponse.From(resource));
    }

    [HttpPost]
    public async Task<ActionResult<ResourceResponse>> Create(CreateResourceRequest request)
    {
        var createdByUserId = _tenantContext.UserId ?? Guid.Empty;
        var resource = await _resourceService.CreateAsync(request.Name, request.Content, createdByUserId);
        return CreatedAtAction(nameof(GetById), new { id = resource.Id }, ResourceResponse.From(resource));
    }
}
