using Microsoft.EntityFrameworkCore;
using TrainHub.Domain.Models;

namespace TrainHub.Infrastructure.Data;

public partial class TrainHubDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Apply Global Query Filters for Tenant Isolation
        modelBuilder.Entity<ApplicationUser>().HasQueryFilter(e => !CurrentOrganizationId.HasValue || e.OrganizationId == CurrentOrganizationId.Value);
        modelBuilder.Entity<AttendanceRecord>().HasQueryFilter(e => !CurrentOrganizationId.HasValue || e.OrganizationId == CurrentOrganizationId.Value);
        modelBuilder.Entity<Course>().HasQueryFilter(e => !CurrentOrganizationId.HasValue || e.OrganizationId == CurrentOrganizationId.Value);
        modelBuilder.Entity<CourseGroup>().HasQueryFilter(e => !CurrentOrganizationId.HasValue || e.OrganizationId == CurrentOrganizationId.Value);
        modelBuilder.Entity<Enrollment>().HasQueryFilter(e => !CurrentOrganizationId.HasValue || e.OrganizationId == CurrentOrganizationId.Value);
        modelBuilder.Entity<Instructor>().HasQueryFilter(e => !CurrentOrganizationId.HasValue || e.OrganizationId == CurrentOrganizationId.Value);
        modelBuilder.Entity<Session>().HasQueryFilter(e => !CurrentOrganizationId.HasValue || e.OrganizationId == CurrentOrganizationId.Value);
        modelBuilder.Entity<Student>().HasQueryFilter(e => !CurrentOrganizationId.HasValue || e.OrganizationId == CurrentOrganizationId.Value);
        modelBuilder.Entity<Organization>().HasQueryFilter(e => !CurrentOrganizationId.HasValue || e.Id == CurrentOrganizationId.Value);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetTenantIdOnAddedEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        SetTenantIdOnAddedEntities();
        return base.SaveChanges();
    }

    private void SetTenantIdOnAddedEntities()
    {
        if (!CurrentOrganizationId.HasValue)
        {
            return;
        }

        var entries = ChangeTracker.Entries().Where(e => e.State == EntityState.Added);
        foreach (var entry in entries)
        {
            var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "OrganizationId");
            if (property != null)
            {
                if (property.CurrentValue is Guid guidValue && guidValue == Guid.Empty)
                {
                    property.CurrentValue = CurrentOrganizationId.Value;
                }
            }
        }
    }
}
