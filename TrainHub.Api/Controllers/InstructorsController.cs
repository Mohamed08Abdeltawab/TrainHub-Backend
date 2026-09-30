using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Instructors.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Api.Controllers;

/// <summary>
/// Manages Instructor profiles and records in the system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InstructorsController : ControllerBase
{
    private readonly IInstructorService _instructorService;
    private readonly IInstructorAuthorizationService _authorizationService;
    private readonly ILogger<InstructorsController> _logger;

    public InstructorsController(
        IInstructorService instructorService,
        IInstructorAuthorizationService authorizationService,
        ILogger<InstructorsController> logger)
    {
        _instructorService = instructorService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a paginated list of instructors with optional search, filtering, and sorting within the current organization.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(PagedResult<InstructorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResult<InstructorDto>>> GetAll(
        [FromQuery] InstructorQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/instructors received.");
        var result = await _instructorService.GetPagedAsync(parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves the profile of the currently authenticated instructor.
    /// </summary>
    [HttpGet("me")]
    [Authorize(Policy = AppPolicies.RequireInstructor)]
    [ProducesResponseType(typeof(InstructorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstructorDto>> GetCurrentInstructor(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            _logger.LogWarning("HTTP GET api/instructors/me received but no valid user ID found in claims.");
            return Unauthorized();
        }

        _logger.LogInformation("HTTP GET api/instructors/me received for user '{UserId}'.", userId.Value);
        var result = await _instructorService.GetCurrentInstructorAsync(userId.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates the profile of the currently authenticated instructor.
    /// </summary>
    [HttpPut("me")]
    [Authorize(Policy = AppPolicies.RequireInstructor)]
    [ProducesResponseType(typeof(InstructorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<InstructorDto>> UpdateCurrentInstructor(
        [FromBody] UpdateInstructorDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            _logger.LogWarning("HTTP PUT api/instructors/me received but no valid user ID found in claims.");
            return Unauthorized();
        }

        _logger.LogInformation("HTTP PUT api/instructors/me received for user '{UserId}'.", userId.Value);
        var result = await _instructorService.UpdateCurrentInstructorAsync(userId.Value, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves an instructor by their unique ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(InstructorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstructorDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/instructors/{Id} received.", id);
        var result = await _instructorService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new instructor record for an existing user.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(InstructorDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<InstructorDto>> Create([FromBody] CreateInstructorDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP POST api/instructors received for user '{UserId}'.", dto.UserId);
        var result = await _instructorService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing instructor record.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(InstructorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<InstructorDto>> Update(Guid id, [FromBody] UpdateInstructorDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP PUT api/instructors/{Id} received.", id);

        var existing = await _instructorService.GetByIdAsync(id, cancellationToken);
        _authorizationService.EnsureCanModifyInstructor(User, existing.UserId);

        var result = await _instructorService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes an instructor if they have no active course groups or sessions.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP DELETE api/instructors/{Id} received.", id);
        await _instructorService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Links an existing instructor to a user account within the same organization.
    /// </summary>
    [HttpPut("{id:guid}/link-user")]
    [HttpPost("{id:guid}/link-user")]
    [Authorize(Policy = AppPolicies.RequireOrgStaff)]
    [ProducesResponseType(typeof(InstructorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<InstructorDto>> LinkUser(
        Guid id,
        [FromBody] LinkInstructorUserDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP PUT api/instructors/{Id}/link-user received for user '{UserId}'.", id, dto.UserId);
        var result = await _instructorService.LinkUserAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves the instructor profile linked to a specific user ID.
    /// </summary>
    [HttpGet("user/{userId:guid}")]
    [Authorize(Policy = AppPolicies.RequireCourseStaff)]
    [ProducesResponseType(typeof(InstructorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstructorDto>> GetByUserId(Guid userId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP GET api/instructors/user/{UserId} received.", userId);
        var result = await _instructorService.GetByUserIdAsync(userId, cancellationToken);
        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub");

        return Guid.TryParse(idClaim, out var userId) ? userId : null;
    }
}
