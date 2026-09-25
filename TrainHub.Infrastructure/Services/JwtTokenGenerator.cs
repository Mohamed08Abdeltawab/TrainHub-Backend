using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TrainHub.Application.Common.Security;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Models;

namespace TrainHub.Infrastructure.Services;

/// <summary>
/// Infrastructure service for generating HMAC-SHA256 signed JSON Web Tokens (JWT).
/// </summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _jwtSettings;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<JwtTokenGenerator> _logger;

    public JwtTokenGenerator(
        IOptions<JwtSettings> jwtOptions,
        UserManager<ApplicationUser> userManager,
        ILogger<JwtTokenGenerator> logger)
    {
        _jwtSettings = jwtOptions.Value;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<string> GenerateTokenAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        _logger.LogInformation("Generating JWT token for user '{UserId}'.", user.Id);

        var roles = await _userManager.GetRolesAsync(user);
        var userClaims = await _userManager.GetClaimsAsync(user);

        return GenerateToken(user, roles, userClaims);
    }

    public string GenerateToken(ApplicationUser user, IEnumerable<string> roles, IEnumerable<Claim>? additionalClaims = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(_jwtSettings.Secret))
        {
            _logger.LogError("JWT Secret key is missing or not configured.");
            throw new InvalidOperationException("JWT Secret key is missing or not configured.");
        }

        if (Encoding.UTF8.GetByteCount(_jwtSettings.Secret) < 32)
        {
            _logger.LogError("JWT Secret key is too short. It must be at least 256 bits (32 bytes).");
            throw new InvalidOperationException("JWT Secret key must be at least 32 characters (256 bits) for HMAC-SHA256 signing.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));//create key
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);//pass key to cardentails

        //payload-البيانات
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),//unique id of user -> sub/NameIdentifier
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),//add type for id
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),//add type for email
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),//username
            new(ClaimTypes.GivenName, user.FirstName ?? string.Empty),
            new(ClaimTypes.Surname, user.LastName ?? string.Empty),
            new("organizationId", user.OrganizationId.ToString()),//must add because trainhub has multi-tanent
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())//unique code for every token
        };

        if (roles != null)
        {
            foreach (var role in roles)//add roles for type 'role'
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim("role", role));//show in json
            }
        }

        if (additionalClaims != null)//if there is more claims need to add
        {
            claims.AddRange(additionalClaims);
        }

        var expiryDuration = _jwtSettings.ExpiryMinutes > 0 ? _jwtSettings.ExpiryMinutes : 60;
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expiryDuration),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}
