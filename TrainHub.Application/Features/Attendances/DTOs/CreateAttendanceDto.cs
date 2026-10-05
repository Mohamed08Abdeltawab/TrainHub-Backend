using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Attendances.DTOs;

/// <summary>
/// Request DTO for recording student attendance for a session.
/// </summary>
public class CreateAttendanceDto
{
    /// <summary>
    /// ID of the session.
    /// </summary>
    [Required(ErrorMessage = "SessionId is required.")]
    public Guid SessionId { get; set; }

    /// <summary>
    /// ID of the student.
    /// </summary>
    [Required(ErrorMessage = "StudentId is required.")]
    public Guid StudentId { get; set; }

    /// <summary>
    /// Attendance status (0 = Absent, 1 = Present, 2 = Late, 3 = Excused).
    /// Defaults to 1 (Present).
    /// </summary>
    public byte Status { get; set; } = AttendanceConstants.DefaultStatus;

    /// <summary>
    /// Optional notes or comments (maximum 250 characters).
    /// </summary>
    [MaxLength(AttendanceConstants.NotesMaxLength, ErrorMessage = "Notes cannot exceed 250 characters.")]
    public string? Notes { get; set; }
}
