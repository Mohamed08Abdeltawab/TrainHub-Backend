using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Instructors.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Service contract for Instructor management operations.
/// </summary>
public interface IInstructorService
{
    Task<PagedResult<InstructorDto>> GetPagedAsync(InstructorQueryParameters parameters, CancellationToken cancellationToken = default);

    Task<InstructorDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<InstructorDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<InstructorDto> CreateAsync(CreateInstructorDto dto, CancellationToken cancellationToken = default);

    Task<InstructorDto> UpdateAsync(Guid id, UpdateInstructorDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<InstructorDto> LinkUserAsync(Guid id, LinkInstructorUserDto dto, CancellationToken cancellationToken = default);

    Task<InstructorDto> LinkUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    Task<InstructorDto> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<InstructorDto> GetCurrentInstructorAsync(Guid currentUserId, CancellationToken cancellationToken = default);

    Task<InstructorDto> UpdateCurrentInstructorAsync(Guid currentUserId, UpdateInstructorDto dto, CancellationToken cancellationToken = default);
}
