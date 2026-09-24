using System;
using System.Collections.Generic;

namespace TrainHub.Domain.Models;

public partial class Enrollment
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CourseGroupId { get; set; }

    public Guid StudentId { get; set; }

    public DateTime EnrollmentDate { get; set; }

    public decimal PaidAmount { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual CourseGroup CourseGroup { get; set; } = null!;

    public virtual Organization Organization { get; set; } = null!;

    public virtual Student Student { get; set; } = null!;
}
