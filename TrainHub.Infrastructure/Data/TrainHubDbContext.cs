using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TrainHub.Domain.Models;

using TrainHub.Application.Interfaces;

namespace TrainHub.Infrastructure.Data;

public partial class TrainHubDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, ITrainHubDbContext
{
    private readonly TrainHub.Application.Interfaces.ICurrentOrganizationService? _currentOrganizationService;

    public TrainHubDbContext()
    {
    }

    public TrainHubDbContext(DbContextOptions<TrainHubDbContext> options, TrainHub.Application.Interfaces.ICurrentOrganizationService? currentOrganizationService = null)
        : base(options)
    {
        _currentOrganizationService = currentOrganizationService;
    }

    public Guid? CurrentOrganizationId => _currentOrganizationService?.OrganizationId;

    public virtual DbSet<ApplicationUser> ApplicationUsers { get; set; }

    public virtual DbSet<AttendanceRecord> AttendanceRecords { get; set; }

    public virtual DbSet<Course> Courses { get; set; }

    public virtual DbSet<CourseGroup> CourseGroups { get; set; }

    public virtual DbSet<Enrollment> Enrollments { get; set; }

    public virtual DbSet<Instructor> Instructors { get; set; }

    public virtual DbSet<Organization> Organizations { get; set; }

    public virtual DbSet<Session> Sessions { get; set; }

    public virtual DbSet<Student> Students { get; set; }
 
    public virtual DbSet<Assignment> Assignments { get; set; }

    /// <summary>
    /// Acquires a transaction-scoped row lock on the specified CourseGroup using UPDLOCK and HOLDLOCK
    /// hints when running against SQL Server, or standard LINQ query when running against non-relational test providers.
    /// </summary>
    public virtual async Task<CourseGroup?> GetCourseGroupWithLockAsync(Guid courseGroupId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (this.Database.IsSqlServer())
        {
            return await CourseGroups
                .FromSqlInterpolated($"SELECT * FROM dbo.CourseGroups WITH (UPDLOCK, HOLDLOCK) WHERE Id = {courseGroupId} AND OrganizationId = {organizationId}")
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await CourseGroups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cg => cg.Id == courseGroupId && cg.OrganizationId == organizationId, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Map ApplicationUser to the existing table name instead of Identity's default "AspNetUsers"
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("ApplicationUsers");
            entity.HasIndex(e => new { e.OrganizationId, e.LastName, e.FirstName }, "IX_ApplicationUsers_Org_LastName_FirstName");

            entity.HasIndex(e => new { e.OrganizationId, e.NormalizedEmail }, "IX_ApplicationUsers_Org_NormalizedEmail").IsUnique();

            entity.HasIndex(e => new { e.OrganizationId, e.NormalizedUserName }, "IX_ApplicationUsers_Org_NormalizedUserName").IsUnique();

            // IdentityDbContext adds a global unique index on NormalizedUserName named "UserNameIndex".
            // In TrainHub SaaS, username/email uniqueness is scoped to OrganizationId via IX_ApplicationUsers_Org_NormalizedUserName.
            // UserNameIndex is retained as a non-unique index to preserve fast lookups without blocking multi-tenant accounts.
            entity.HasIndex(e => e.NormalizedUserName)
                .HasDatabaseName("UserNameIndex")
                .IsUnique(false);

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ApplicationUsers_CreatedAt");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_ApplicationUsers_IsActive");
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.LockoutEnabled).HasDefaultValue(true, "DF_ApplicationUsers_LockoutEnabled");
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.PhoneNumber).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ApplicationUsers_UpdatedAt");
            entity.Property(e => e.UserName).HasMaxLength(256);

            entity.HasOne(d => d.Organization).WithMany(p => p.ApplicationUsers)
                .HasForeignKey(d => d.OrganizationId)
                .HasConstraintName("FK_ApplicationUsers_Organizations");
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.StudentId }, "IX_AttendanceRecords_Org_StudentId");

            entity.HasIndex(e => e.OrganizationId, "IX_AttendanceRecords_OrganizationId");

            entity.HasIndex(e => new { e.SessionId, e.StudentId }, "IX_AttendanceRecords_Session_Student").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.MarkedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_AttendanceRecords_MarkedAt");
            entity.Property(e => e.Notes).HasMaxLength(250);
            entity.Property(e => e.Status).HasDefaultValue((byte)1, "DF_AttendanceRecords_Status");

            entity.HasOne(d => d.MarkedByUser).WithMany(p => p.AttendanceRecords)
                .HasForeignKey(d => d.MarkedByUserId)
                .HasConstraintName("FK_AttendanceRecords_MarkedBy");

            entity.HasOne(d => d.Organization).WithMany(p => p.AttendanceRecords)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AttendanceRecords_Organizations");

            entity.HasOne(d => d.Session).WithMany(p => p.AttendanceRecords)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("FK_AttendanceRecords_Sessions");

            entity.HasOne(d => d.Student).WithMany(p => p.AttendanceRecords)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AttendanceRecords_Students");
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.Name }, "IX_Courses_Org_Name").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.BasePrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Courses_CreatedAt");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Status).HasDefaultValue((byte)1, "DF_Courses_Status");
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Courses_UpdatedAt");

            entity.HasOne(d => d.Organization).WithMany(p => p.Courses)
                .HasForeignKey(d => d.OrganizationId)
                .HasConstraintName("FK_Courses_Organizations");
        });

        modelBuilder.Entity<CourseGroup>(entity =>
        {
            entity.HasIndex(e => e.CourseId, "IX_CourseGroups_CourseId");

            entity.HasIndex(e => e.InstructorId, "IX_CourseGroups_InstructorId");

            entity.HasIndex(e => e.OrganizationId, "IX_CourseGroups_OrganizationId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Capacity).HasDefaultValue(30, "DF_CourseGroups_Capacity");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_CourseGroups_CreatedAt");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.ScheduleDescription).HasMaxLength(250);
            entity.Property(e => e.Status).HasDefaultValue((byte)1, "DF_CourseGroups_Status");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_CourseGroups_UpdatedAt");

            entity.HasOne(d => d.Course).WithMany(p => p.CourseGroups)
                .HasForeignKey(d => d.CourseId)
                .HasConstraintName("FK_CourseGroups_Courses");

            entity.HasOne(d => d.Instructor).WithMany(p => p.CourseGroups)
                .HasForeignKey(d => d.InstructorId)
                .HasConstraintName("FK_CourseGroups_Instructors");

            entity.HasOne(d => d.Organization).WithMany(p => p.CourseGroups)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CourseGroups_Organizations");
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.HasIndex(e => new { e.CourseGroupId, e.Status }, "IX_Enrollments_CourseGroupId_Status");

            entity.HasIndex(e => new { e.CourseGroupId, e.StudentId }, "IX_Enrollments_Group_Student").IsUnique();

            entity.HasIndex(e => e.OrganizationId, "IX_Enrollments_OrganizationId");

            entity.HasIndex(e => e.StudentId, "IX_Enrollments_StudentId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Enrollments_CreatedAt");
            entity.Property(e => e.EnrollmentDate).HasDefaultValueSql("(sysutcdatetime())", "DF_Enrollments_EnrollmentDate");
            entity.Property(e => e.PaidAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status).HasDefaultValue((byte)1, "DF_Enrollments_Status");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Enrollments_UpdatedAt");

            entity.HasOne(d => d.CourseGroup).WithMany(p => p.Enrollments)
                .HasForeignKey(d => d.CourseGroupId)
                .HasConstraintName("FK_Enrollments_CourseGroups");

            entity.HasOne(d => d.Organization).WithMany(p => p.Enrollments)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Enrollments_Organizations");

            entity.HasOne(d => d.Student).WithMany(p => p.Enrollments)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Enrollments_Students");
        });

        modelBuilder.Entity<Instructor>(entity =>
        {
            entity.HasIndex(e => e.OrganizationId, "IX_Instructors_OrganizationId");

            entity.HasIndex(e => e.UserId, "IX_Instructors_UserId").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Instructors_CreatedAt");
            entity.Property(e => e.Specialization).HasMaxLength(150);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Instructors_UpdatedAt");

            entity.HasOne(d => d.Organization).WithMany(p => p.Instructors)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Instructors_Organizations");

            entity.HasOne(d => d.User).WithOne(p => p.Instructor)
                .HasForeignKey<Instructor>(d => d.UserId)
                .HasConstraintName("FK_Instructors_ApplicationUsers");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasIndex(e => e.Slug, "IX_Organizations_Slug").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Organizations_CreatedAt");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Organizations_IsActive");
            entity.Property(e => e.LogoUrl).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Slug).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Organizations_UpdatedAt");
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasIndex(e => e.CourseGroupId, "IX_Sessions_CourseGroupId");

            entity.HasIndex(e => e.InstructorId, "IX_Sessions_InstructorId");

            entity.HasIndex(e => new { e.OrganizationId, e.StartTime }, "IX_Sessions_Org_StartTime");

            entity.HasIndex(e => e.OrganizationId, "IX_Sessions_OrganizationId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Sessions_CreatedAt");
            entity.Property(e => e.Room).HasMaxLength(50);
            entity.Property(e => e.Status).HasDefaultValue((byte)1, "DF_Sessions_Status");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Sessions_UpdatedAt");

            entity.HasOne(d => d.CourseGroup).WithMany(p => p.Sessions)
                .HasForeignKey(d => d.CourseGroupId)
                .HasConstraintName("FK_Sessions_CourseGroups");

            entity.HasOne(d => d.Instructor).WithMany(p => p.Sessions)
                .HasForeignKey(d => d.InstructorId)
                .HasConstraintName("FK_Sessions_Instructors");

            entity.HasOne(d => d.Organization).WithMany(p => p.Sessions)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sessions_Organizations");
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.StudentCode }, "IX_Students_Org_StudentCode").IsUnique();

            entity.HasIndex(e => e.UserId, "IX_Students_UserId").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.AcademicYear).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Students_CreatedAt");
            entity.Property(e => e.GuardianPhone).HasMaxLength(20);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.StudentCode).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Students_UpdatedAt");

            entity.HasOne(d => d.Organization).WithMany(p => p.Students)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Students_Organizations");

            entity.HasOne(d => d.User).WithOne(p => p.Student)
                .HasForeignKey<Student>(d => d.UserId)
                .HasConstraintName("FK_Students_ApplicationUsers");
        });

        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.HasIndex(e => e.OrganizationId, "IX_Assignments_OrganizationId");
            entity.HasIndex(e => e.CourseGroupId, "IX_Assignments_CourseGroupId");

            entity.Property(e => e.Id).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.MaxScore).HasDefaultValue(100, "DF_Assignments_MaxScore");
            entity.Property(e => e.Status).HasDefaultValue((byte)1, "DF_Assignments_Status");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Assignments_CreatedAt");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_Assignments_UpdatedAt");

            entity.HasOne(d => d.CourseGroup).WithMany()
                .HasForeignKey(d => d.CourseGroupId)
                .HasConstraintName("FK_Assignments_CourseGroups");

            entity.HasOne(d => d.Organization).WithMany()
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Assignments_Organizations");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
