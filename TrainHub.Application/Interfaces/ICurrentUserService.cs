using TrainHub.Application.Features.Users.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Contract for retrieving the currently authenticated user's profile.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Retrieves the profile of the currently authenticated user by their ID.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user (from JWT claims).</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A <see cref="CurrentUserResponse"/> containing the user's profile data.</returns>
    Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
