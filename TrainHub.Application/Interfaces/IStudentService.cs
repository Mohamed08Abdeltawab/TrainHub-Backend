using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.CourseGroups.DTOs;
using TrainHub.Application.Features.Sessions.DTOs;
using TrainHub.Application.Features.Students.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Service contract for Student management operations.
/// </summary>
public interface IStudentService
{
    Task<PagedResult<StudentDto>> GetPagedAsync(StudentQueryParameters parameters, CancellationToken cancellationToken = default);

    Task<StudentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<StudentDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<StudentDto> CreateAsync(CreateStudentDto dto, CancellationToken cancellationToken = default);

    Task<StudentDto> UpdateAsync(Guid id, UpdateStudentDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<StudentDto> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IEnumerable<StudentCourseDto>> GetMyCoursesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IEnumerable<CourseGroupDto>> GetMyCohortsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IEnumerable<SessionDto>> GetMySessionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
