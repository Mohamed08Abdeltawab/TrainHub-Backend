using TrainHub.Application.Features.Auth.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Contract for authentication operations (register, login, token refresh, etc.).
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new user, assigns the default role, generates tokens, and returns an authentication response.
    /// </summary>
    /// <param name="request">The registration request data.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>An <see cref="AuthResponse"/> containing user info and tokens.</returns>
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates a user and returns an authentication response with tokens.
    /// </summary>
    /// <param name="request">The login request data.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>An <see cref="AuthResponse"/> containing user info and tokens.</returns>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// </summary>
    /// <param name="request">The refresh token request data.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>An <see cref="AuthResponse"/> containing new tokens.</returns>
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
}
