using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Assignments.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Service interface for managing Assignment operations.
/// </summary>
public interface IAssignmentService
{
    Task<PagedResult<AssignmentDto>> GetPagedAsync(AssignmentQueryParameters parameters, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    Task<AssignmentDto> GetByIdAsync(Guid id, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    Task<AssignmentDto> CreateAsync(CreateAssignmentDto dto, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    Task<AssignmentDto> UpdateAsync(Guid id, UpdateAssignmentDto dto, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, Guid? currentUserId = null, CancellationToken cancellationToken = default);
}
