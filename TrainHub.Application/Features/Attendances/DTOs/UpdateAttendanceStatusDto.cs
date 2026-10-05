using System.ComponentModel.DataAnnotations;

namespace TrainHub.Application.Features.Attendances.DTOs;

/// <summary>
/// Request DTO for updating the status of an attendance record.
/// </summary>
public class UpdateAttendanceStatusDto
{
    /// <summary>
    /// Updated attendance status (0 = Absent, 1 = Present, 2 = Late, 3 = Excused).
    /// </summary>
    [Required(ErrorMessage = "Status is required.")]
    public byte Status { get; set; }
}
