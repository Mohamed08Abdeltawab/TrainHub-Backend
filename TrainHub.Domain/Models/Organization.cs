using System;
using System.Collections.Generic;

namespace TrainHub.Domain.Models;

public partial class Organization
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? LogoUrl { get; set; }

    public string SubscriptionPlan { get; set; } = null!;

    public DateTime SubscriptionEndsAt { get; set; }

    public string SubscriptionStatus { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<ApplicationUser> ApplicationUsers { get; set; } = new List<ApplicationUser>();

    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    public virtual ICollection<CourseGroup> CourseGroups { get; set; } = new List<CourseGroup>();

    public virtual ICollection<Course> Courses { get; set; } = new List<Course>();

    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    public virtual ICollection<Instructor> Instructors { get; set; } = new List<Instructor>();

    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();

    public virtual ICollection<Student> Students { get; set; } = new List<Student>();
}
