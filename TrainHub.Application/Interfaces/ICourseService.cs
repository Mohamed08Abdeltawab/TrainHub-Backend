using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Courses.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Service contract for Course management operations.
/// </summary>
public interface ICourseService
{
    Task<PagedResult<CourseDto>> GetPagedAsync(CourseQueryParameters parameters, CancellationToken cancellationToken = default);

    Task<CourseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<CourseDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<CourseDto> CreateAsync(CreateCourseDto dto, CancellationToken cancellationToken = default);

    Task<CourseDto> UpdateAsync(Guid id, UpdateCourseDto dto, CancellationToken cancellationToken = default);

    Task<CourseDto> UpdateStatusAsync(Guid id, UpdateCourseStatusDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
