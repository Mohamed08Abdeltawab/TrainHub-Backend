using System;
using System.Collections.Generic;

namespace TrainHub.Domain.Models;

public partial class Course
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? Category { get; set; }

    public decimal BasePrice { get; set; }

    public int? DurationInHours { get; set; }

    public string? ThumbnailUrl { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<CourseGroup> CourseGroups { get; set; } = new List<CourseGroup>();

    public virtual Organization Organization { get; set; } = null!;
}
