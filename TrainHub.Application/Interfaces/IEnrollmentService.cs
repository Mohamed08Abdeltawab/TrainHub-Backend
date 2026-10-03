using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Enrollments.DTOs;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Service contract for managing Enrollment entities and operations.
/// </summary>
public interface IEnrollmentService
{
    /// <summary>
    /// Retrieves a paginated list of enrollments matching the query criteria within the current organization.
    /// </summary>
    Task<PagedResult<EnrollmentDto>> GetPagedAsync(EnrollmentQueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an enrollment by its ID within the current organization.
    /// </summary>
    Task<EnrollmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enrolls a student into a course group in the current organization.
    /// </summary>
    Task<EnrollmentDto> CreateAsync(CreateEnrollmentDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates details of an existing enrollment.
    /// </summary>
    Task<EnrollmentDto> UpdateAsync(Guid id, UpdateEnrollmentDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of an existing enrollment.
    /// </summary>
    Task<EnrollmentDto> UpdateStatusAsync(Guid id, UpdateEnrollmentStatusDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an enrollment record if no dependent attendance records exist.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
