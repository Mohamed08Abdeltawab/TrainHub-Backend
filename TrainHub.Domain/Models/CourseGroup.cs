using System;
using System.Collections.Generic;

namespace TrainHub.Domain.Models;

public partial class CourseGroup
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CourseId { get; set; }

    public Guid? InstructorId { get; set; }

    public string Name { get; set; } = null!;

    public string? ScheduleDescription { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int Capacity { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Course Course { get; set; } = null!;

    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    public virtual Instructor? Instructor { get; set; }

    public virtual Organization Organization { get; set; } = null!;

    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
}
