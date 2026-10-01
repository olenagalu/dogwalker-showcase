using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using PawsAndPaths.Api.Models;

namespace PawsAndPaths.Api.Services;

public interface ITokenService
{
    Task<(string Token, DateTimeOffset ExpiresAt)> CreateAsync(AppUser user);
}

public class TokenService(UserManager<AppUser> userManager, IConfiguration configuration) : ITokenService
{
    public async Task<(string Token, DateTimeOffset ExpiresAt)> CreateAsync(AppUser user)
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = configuration["Jwt:Issuer"] ?? "PrincessDogWalker";
        var audience = configuration["Jwt:Audience"] ?? "PrincessDogWalker.Web";
        var expiresAt = DateTimeOffset.UtcNow.AddHours(8);
        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer, audience, claims, expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
