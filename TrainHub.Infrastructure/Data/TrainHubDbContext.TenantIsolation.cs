using Microsoft.EntityFrameworkCore;
using TrainHub.Domain.Models;

namespace TrainHub.Infrastructure.Data;

public partial class TrainHubDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Apply Global Query Filters for Tenant Isolation
        modelBuilder.Entity<ApplicationUser>().HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == CurrentOrganizationId);
        modelBuilder.Entity<AttendanceRecord>().HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == CurrentOrganizationId);
        modelBuilder.Entity<Course>().HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == CurrentOrganizationId);
        modelBuilder.Entity<CourseGroup>().HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == CurrentOrganizationId);
        modelBuilder.Entity<Enrollment>().HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == CurrentOrganizationId);
        modelBuilder.Entity<Instructor>().HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == CurrentOrganizationId);
        modelBuilder.Entity<Session>().HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == CurrentOrganizationId);
        modelBuilder.Entity<Student>().HasQueryFilter(e => CurrentOrganizationId == null || e.OrganizationId == CurrentOrganizationId);
        modelBuilder.Entity<Organization>().HasQueryFilter(e => CurrentOrganizationId == null || e.Id == CurrentOrganizationId);
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
