using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainHub.Application.Features.Organizations.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.RequireSuperAdmin)] // Restricting for now, assuming only super admins can manage orgs entirely. Adjust as needed.
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(IOrganizationService organizationService, ILogger<OrganizationsController> logger)
    {
        _organizationService = organizationService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OrganizationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OrganizationDto>>> GetAll(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting all organizations");
        var result = await _organizationService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting organization by id {Id}", id);
        var result = await _organizationService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrganizationDto>> Create([FromBody] CreateOrganizationDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating organization");
        var result = await _organizationService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> Update(Guid id, [FromBody] UpdateOrganizationDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating organization {Id}", id);
        var result = await _organizationService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting organization {Id}", id);
        await _organizationService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
