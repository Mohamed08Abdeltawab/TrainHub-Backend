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
}
