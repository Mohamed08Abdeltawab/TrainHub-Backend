using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Attendances.DTOs;

/// <summary>
/// Request DTO for updating an existing attendance record.
/// </summary>
public class UpdateAttendanceDto
{
    /// <summary>
    /// Optional updated status (0 = Absent, 1 = Present, 2 = Late, 3 = Excused).
    /// </summary>
    public byte? Status { get; set; }

    /// <summary>
    /// Optional updated notes or comments (maximum 250 characters).
    /// </summary>
    [MaxLength(AttendanceConstants.NotesMaxLength, ErrorMessage = "Notes cannot exceed 250 characters.")]
    public string? Notes { get; set; }
}
