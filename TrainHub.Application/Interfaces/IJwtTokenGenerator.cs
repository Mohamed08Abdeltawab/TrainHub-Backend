using System.Security.Claims;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Contract for generating JSON Web Tokens (JWT) for authenticated users.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Generates a signed JWT token string for the specified user, including their assigned roles and custom claims.
    /// </summary>
    /// <param name="user">The application user for whom the token is generated.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A signed JWT token string.</returns>
    Task<string> GenerateTokenAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a signed JWT token string for the specified user and explicit roles.
    /// </summary>
    /// <param name="user">The application user for whom the token is generated.</param>
    /// <param name="roles">The roles assigned to the user.</param>
    /// <param name="additionalClaims">Optional additional claims to include in the token payload.</param>
    /// <returns>A signed JWT token string.</returns>
    string GenerateToken(ApplicationUser user, IEnumerable<string> roles, IEnumerable<Claim>? additionalClaims = null);
}
