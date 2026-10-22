using Microsoft.EntityFrameworkCore;
using TenantSubscriptionApi.Contracts.Responses;
using TenantSubscriptionApi.Data.Public;

namespace TenantSubscriptionApi.Services.Platform;

// Constructor takes PublicDbContext ONLY — there is no TenantDbContext parameter
// here, and there never should be. This is the .NET analogue of the reference
// implementation's "import discipline": a platform-admin class that is structurally
// incapable of reaching tenant content, not just permission-checked against it.
// Enforced automatically by ArchitectureTests.NonTenantServices_NeverDependOn_TenantDbContext.
public class PlatformAdminService
{
    private readonly PublicDbContext _publicDb;

    public PlatformAdminService(PublicDbContext publicDb)
    {
        _publicDb = publicDb;
    }

    public async Task<PagedResult<OrganizationSummary>> ListOrganizationsAsync(int page, int pageSize)
    {
        var query = _publicDb.Organizations.Include(o => o.Plan).OrderBy(o => o.Name);

        var totalCount = await query.CountAsync();

        // Projecting into the DTO here (not returning tracked Organization entities)
        // means a future navigation property added to Organization can't silently
        // widen what a platform admin sees — the shape is pinned at this one line.
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrganizationSummary(
                o.Id, o.Name, o.Plan.Name, o.UserCount, o.Plan.MaxUsers, o.StorageUsedMB, o.Plan.MaxStorageMB))
            .ToListAsync();

        return new PagedResult<OrganizationSummary>(items, totalCount, page, pageSize);
    }
}
