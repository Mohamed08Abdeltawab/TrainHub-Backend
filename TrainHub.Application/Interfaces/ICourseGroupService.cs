using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.CourseGroups.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Service interface for managing CourseGroup operations.
/// </summary>
public interface ICourseGroupService
{
    Task<PagedResult<CourseGroupDto>> GetPagedAsync(CourseGroupQueryParameters parameters, CancellationToken cancellationToken = default);

    Task<CourseGroupDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CourseGroupDto> CreateAsync(CreateCourseGroupDto dto, CancellationToken cancellationToken = default);

    Task<CourseGroupDto> UpdateAsync(Guid id, UpdateCourseGroupDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
