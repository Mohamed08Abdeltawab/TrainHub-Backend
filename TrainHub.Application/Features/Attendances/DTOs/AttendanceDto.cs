namespace TrainHub.Application.Features.Attendances.DTOs;

/// <summary>
/// Response DTO representing an Attendance record.
/// </summary>
public class AttendanceDto
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid SessionId { get; set; }

    public Guid StudentId { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public string StudentCode { get; set; } = string.Empty;

    public Guid? MarkedByUserId { get; set; }

    public string? MarkedByName { get; set; }

    public byte Status { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public DateTime MarkedAt { get; set; }

    public string? Notes { get; set; }

    public string? SessionTitle { get; set; }

    public string? CourseGroupName { get; set; }

    public DateTime? SessionDate { get; set; }
}
