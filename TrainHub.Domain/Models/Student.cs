using System;
using System.Collections.Generic;

namespace TrainHub.Domain.Models;

public partial class Student
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid OrganizationId { get; set; }

    public string StudentCode { get; set; } = null!;

    public string? GuardianPhone { get; set; }

    public string? AcademicYear { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    public virtual Organization Organization { get; set; } = null!;

    public virtual ApplicationUser User { get; set; } = null!;
}
