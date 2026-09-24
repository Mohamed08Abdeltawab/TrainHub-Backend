using System;
using System.Collections.Generic;

namespace TrainHub.Domain.Models;

public partial class AttendanceRecord
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid SessionId { get; set; }

    public Guid StudentId { get; set; }

    public Guid? MarkedByUserId { get; set; }

    public byte Status { get; set; }

    public DateTime MarkedAt { get; set; }

    public string? Notes { get; set; }

    public virtual ApplicationUser? MarkedByUser { get; set; }

    public virtual Organization Organization { get; set; } = null!;

    public virtual Session Session { get; set; } = null!;

    public virtual Student Student { get; set; } = null!;
}
