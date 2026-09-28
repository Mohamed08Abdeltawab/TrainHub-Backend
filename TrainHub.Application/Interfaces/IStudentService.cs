using TrainHub.Application.Features.Students.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Service contract for Student management operations.
/// </summary>
public interface IStudentService
{
    Task<StudentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<StudentDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<StudentDto> CreateAsync(CreateStudentDto dto, CancellationToken cancellationToken = default);

    Task<StudentDto> UpdateAsync(Guid id, UpdateStudentDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
