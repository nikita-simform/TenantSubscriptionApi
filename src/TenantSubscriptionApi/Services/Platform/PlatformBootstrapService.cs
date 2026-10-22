using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TenantSubscriptionApi.Data.Public;
using TenantSubscriptionApi.Domain.Entities;
using TenantSubscriptionApi.Domain.Enums;
using TenantSubscriptionApi.Domain.Exceptions;

namespace TenantSubscriptionApi.Services.Platform;

public class PlatformBootstrapService
{
    private readonly PublicDbContext _publicDb;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public PlatformBootstrapService(PublicDbContext publicDb)
    {
        _publicDb = publicDb;
    }

    // Idempotent: safe to call on every startup. Refuses to silently "promote" an
    // existing org-scoped user into a platform admin — that would be a privilege
    // escalation nobody asked for, not a convenience.
    public async Task<bool> EnsurePlatformAdminAsync(string email, string password)
    {
        var existing = await _publicDb.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existing is not null)
        {
            if (existing.Role != UserRole.PlatformAdmin)
                throw new ValidationException(
                    $"'{email}' already belongs to a non-platform-admin user; refusing to silently promote it.");
            return false;
        }

        var admin = new User { Id = Guid.NewGuid(), Email = email, Role = UserRole.PlatformAdmin, OrganizationId = null };
        admin.PasswordHash = _passwordHasher.HashPassword(admin, password);

        _publicDb.Users.Add(admin);
        await _publicDb.SaveChangesAsync();
        return true;
    }
}
