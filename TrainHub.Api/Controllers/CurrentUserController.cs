using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainHub.Application.Features.Users.DTOs;
using TrainHub.Application.Interfaces;

namespace TrainHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CurrentUserController : ControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CurrentUserController> _logger;

    public CurrentUserController(ICurrentUserService currentUserService, ILogger<CurrentUserController> logger)
    {
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Returns the profile of the currently authenticated user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("HTTP GET api/currentuser received but no valid user ID found in token claims.");
            return Unauthorized();
        }

        _logger.LogInformation("HTTP GET api/currentuser received for user '{UserId}'.", userId);
        var response = await _currentUserService.GetCurrentUserAsync(userId, cancellationToken);
        return Ok(response);
    }
}
