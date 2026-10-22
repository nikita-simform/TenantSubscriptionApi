using Microsoft.EntityFrameworkCore;
using TenantSubscriptionApi.Data.Tenant;
using TenantSubscriptionApi.Domain.Entities;

namespace TenantSubscriptionApi.Services.Tenant;

// This namespace is the ONLY one that depends on TenantDbContext — kept separate
// from Services.Platform so the structural blindness guarantee (see
// PlatformAdminService) has something meaningful to contrast against.
public class ResourceService
{
    private readonly TenantDbContext _tenantDb;

    public ResourceService(TenantDbContext tenantDb)
    {
        _tenantDb = tenantDb;
    }

    public async Task<List<Resource>> GetAllAsync() =>
        await _tenantDb.Resources.OrderByDescending(r => r.CreatedAt).ToListAsync();

    public async Task<Resource?> GetByIdAsync(Guid id) =>
        await _tenantDb.Resources.FindAsync(id);

    public async Task<Resource> CreateAsync(string name, string content, Guid createdByUserId)
    {
        var resource = new Resource
        {
            Id = Guid.NewGuid(),
            Name = name,
            Content = content,
            SizeBytes = System.Text.Encoding.UTF8.GetByteCount(content),
            CreatedByUserId = createdByUserId
        };

        _tenantDb.Resources.Add(resource);
        await _tenantDb.SaveChangesAsync();
        return resource;
    }
}
