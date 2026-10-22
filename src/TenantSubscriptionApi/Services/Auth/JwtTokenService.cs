using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TenantSubscriptionApi.Domain.Enums;

namespace TenantSubscriptionApi.Services.Auth;

// Issues the token TenantResolutionMiddleware later reads back into ITenantContext.
// organizationId/schemaName claims are omitted entirely for PlatformAdmin — there is
// nothing for a platform admin's token to scope to.
public class JwtTokenService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _secret = configuration["Jwt:Secret"]!;
        _issuer = configuration["Jwt:Issuer"]!;
        _audience = configuration["Jwt:Audience"]!;
        _expiryMinutes = int.Parse(configuration["Jwt:ExpiryMinutes"]!);
    }

    public string GenerateToken(Guid userId, UserRole role, Guid? organizationId, string? schemaName)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.Role, role.ToString())
        };

        if (organizationId is not null)
            claims.Add(new Claim("organizationId", organizationId.Value.ToString()));
        if (schemaName is not null)
            claims.Add(new Claim("schemaName", schemaName));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
