using TrainHub.Application.Features.CourseGroups.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Defines validation contracts for CourseGroup operations.
/// </summary>
public interface ICourseGroupValidator
{
    void ValidateQuery(CourseGroupQueryParameters query);

    Task ValidateCreateAsync(CreateCourseGroupDto dto, Guid organizationId, CancellationToken cancellationToken = default);

    Task ValidateUpdateAsync(Guid id, UpdateCourseGroupDto dto, Guid organizationId, CancellationToken cancellationToken = default);

    Task ValidateDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
