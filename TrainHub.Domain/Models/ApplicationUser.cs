using Microsoft.AspNetCore.Identity;

namespace TrainHub.Domain.Models;

public partial class ApplicationUser : IdentityUser<Guid>
{
    public Guid OrganizationId { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    public virtual Instructor? Instructor { get; set; }

    public virtual Organization Organization { get; set; } = null!;

    public virtual Student? Student { get; set; }
}
