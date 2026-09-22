using System;
using System.Collections.Generic;

namespace TrainHub.Domain.Models;

public partial class Session
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CourseGroupId { get; set; }

    public Guid? InstructorId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Room { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    public virtual CourseGroup CourseGroup { get; set; } = null!;

    public virtual Instructor? Instructor { get; set; }

    public virtual Organization Organization { get; set; } = null!;
}
