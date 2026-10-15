using TrainHub.Application.Features.Assignments.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Defines validation contracts for Assignment operations.
/// </summary>
public interface IAssignmentValidator
{
    void ValidateQuery(AssignmentQueryParameters query);

    Task ValidateCreateAsync(CreateAssignmentDto dto, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    Task ValidateUpdateAsync(Guid id, UpdateAssignmentDto dto, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);

    Task ValidateDeleteAsync(Guid id, Guid organizationId, Guid? currentUserId = null, CancellationToken cancellationToken = default);
}
