using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Interfaces;

/// <summary>
/// Abstraction for database operations and queryable entity sets.
/// Implemented by TrainHubDbContext in the Infrastructure layer.
/// </summary>
public interface ITrainHubDbContext
{
    DbSet<ApplicationUser> ApplicationUsers { get; }
    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<Course> Courses { get; }
    DbSet<CourseGroup> CourseGroups { get; }
    DbSet<Enrollment> Enrollments { get; }
    DbSet<Instructor> Instructors { get; }
    DbSet<Organization> Organizations { get; }
    DbSet<Session> Sessions { get; }
    DbSet<Student> Students { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
