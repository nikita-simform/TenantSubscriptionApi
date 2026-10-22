using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TenantSubscriptionApi.Data.Public;
using TenantSubscriptionApi.Domain.Entities;
using TenantSubscriptionApi.Domain.Exceptions;

namespace TenantSubscriptionApi.Services.Auth;

public class AuthService
{
    private readonly PublicDbContext _publicDb;
    private readonly JwtTokenService _jwtTokenService;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthService(PublicDbContext publicDb, JwtTokenService jwtTokenService)
    {
        _publicDb = publicDb;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<string> LoginAsync(string email, string password)
    {
        var user = await _publicDb.Users.Include(u => u.Organization).FirstOrDefaultAsync(u => u.Email == email);

        // Deliberately the SAME error for "no such user" and "wrong password" —
        // distinguishing them would let an attacker enumerate valid emails.
        if (user is null || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
            throw new ValidationException("Invalid email or password.");

        return _jwtTokenService.GenerateToken(user.Id, user.Role, user.OrganizationId, user.Organization?.SchemaName);
    }
}
