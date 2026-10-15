using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TrainHub.Application.Features.Attendances.DTOs;

/// <summary>
/// Request DTO for recording batch attendance for a session.
/// </summary>
public class BatchAttendanceRequestDto
{
    /// <summary>
    /// List of attendance records for students in the session.
    /// </summary>
    [Required(ErrorMessage = "Attendance records are required.")]
    [JsonPropertyName("records")]
    public List<BatchAttendanceItemDto> Records { get; set; } = [];

    /// <summary>
    /// Alias for Records to support 'items' naming if submitted by client.
    /// </summary>
    [JsonPropertyName("items")]
    public List<BatchAttendanceItemDto>? Items
    {
        get => Records;
        set
        {
            if (value != null && Records.Count == 0)
            {
                Records = value;
            }
        }
    }
}
