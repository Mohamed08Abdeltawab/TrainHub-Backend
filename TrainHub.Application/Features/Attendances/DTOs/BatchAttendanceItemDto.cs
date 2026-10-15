using System.ComponentModel.DataAnnotations;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Features.Attendances.DTOs;

/// <summary>
/// DTO representing an individual student's attendance record in a batch submission.
/// </summary>
public class BatchAttendanceItemDto
{
    /// <summary>
    /// ID of the student.
    /// </summary>
    [Required(ErrorMessage = "StudentId is required.")]
    public Guid StudentId { get; set; }

    /// <summary>
    /// Attendance status (0 = Absent, 1 = Present, 2 = Late, 3 = Excused).
    /// Defaults to 1 (Present).
    /// </summary>
    [Range(0, 3, ErrorMessage = "Status must be a valid attendance status (0 = Absent, 1 = Present, 2 = Late, 3 = Excused).")]
    public byte Status { get; set; } = AttendanceConstants.DefaultStatus;

    /// <summary>
    /// Optional notes or comments (maximum 250 characters).
    /// </summary>
    [MaxLength(AttendanceConstants.NotesMaxLength, ErrorMessage = "Notes cannot exceed 250 characters.")]
    public string? Notes { get; set; }
}
