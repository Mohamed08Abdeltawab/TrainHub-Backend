using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Enrollments.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.IntegrationTests.Infrastructure;
using Xunit;

namespace TrainHub.IntegrationTests.Integrity;

[Collection("SqlServer")]
public class SqlServerDataIntegrityAndRollbackTests : IClassFixture<SqlServerTestFixture>
{
    private readonly SqlServerTestFixture _fixture;

    public SqlServerDataIntegrityAndRollbackTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DuplicateEnrollment_SameStudentAndGroup_RejectsAndEnforcesIntegrity()
    {
        var orgId = Guid.NewGuid();
        Guid groupId;
        Guid studentId;

        using (var setupContext = _fixture.CreateNewDbContext(orgId))
        {
            var org = new Organization
            {
                Id = orgId,
                Name = $"Duplicate Org {Guid.NewGuid():N}",
                Slug = $"dup-org-{Guid.NewGuid():N}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            setupContext.Organizations.Add(org);

            var course = Course.Create(orgId, "Duplicate Invariant Course");
            setupContext.Courses.Add(course);

            var group = CourseGroup.Create(orgId, course.Id, "Group Dup", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), capacity: 10);
            setupContext.CourseGroups.Add(group);

            var user = new ApplicationUser { Id = Guid.NewGuid(), OrganizationId = orgId, FirstName = "Student", LastName = "Dup", Email = $"dup_{Guid.NewGuid():N}@test.com", UserName = $"dup_{Guid.NewGuid():N}@test.com", IsActive = true };
            setupContext.ApplicationUsers.Add(user);

            var student = Student.Create(user.Id, orgId, "STU-DUP");
            setupContext.Students.Add(student);

            await setupContext.SaveChangesAsync();

            groupId = group.Id;
            studentId = student.Id;
        }

        using var scope = _fixture.CreateScope(orgId);
        var service = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();

        var dto = new CreateEnrollmentDto
        {
            CourseGroupId = groupId,
            StudentId = studentId,
            PaidAmount = 50m,
            Status = EnrollmentConstants.Statuses.Active,
            EnrollmentDate = DateTime.UtcNow
        };

        // First enrollment succeeds
        var first = await service.CreateAsync(dto);
        first.Should().NotBeNull();

        // Second enrollment for same student and group must throw ConflictException
        var act = () => service.CreateAsync(dto);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already enrolled in this course group*");

        // Verify only 1 enrollment exists in SQL Server
        using var verifyContext = _fixture.CreateNewDbContext(orgId);
        var count = await verifyContext.Enrollments
            .IgnoreQueryFilters()
            .CountAsync(e => e.CourseGroupId == groupId && e.StudentId == studentId);

        count.Should().Be(1);
    }

    [Fact]
    public async Task DirectDuplicateEnrollmentInsert_ViolatesUniqueIndexInDatabase()
    {
        var orgId = Guid.NewGuid();
        Guid groupId;
        Guid studentId;

        using (var context = _fixture.CreateNewDbContext(orgId))
        {
            var org = new Organization { Id = orgId, Name = $"Constraint Org {Guid.NewGuid():N}", Slug = $"cnst-{Guid.NewGuid():N}", IsActive = true };
            context.Organizations.Add(org);
            var course = Course.Create(orgId, "DB Constraint Course");
            context.Courses.Add(course);
            var group = CourseGroup.Create(orgId, course.Id, "Group", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), capacity: 10);
            context.CourseGroups.Add(group);
            var user = new ApplicationUser { Id = Guid.NewGuid(), OrganizationId = orgId, FirstName = "S", LastName = "U", Email = $"u_{Guid.NewGuid():N}@test.com", UserName = $"u_{Guid.NewGuid():N}@test.com", IsActive = true };
            context.ApplicationUsers.Add(user);
            var student = Student.Create(user.Id, orgId, "STU-001");
            context.Students.Add(student);
            await context.SaveChangesAsync();

            groupId = group.Id;
            studentId = student.Id;

            var e1 = Enrollment.Create(orgId, groupId, studentId, 0m, EnrollmentConstants.Statuses.Active);
            context.Enrollments.Add(e1);
            await context.SaveChangesAsync();

            // Direct attempt to insert duplicate (CourseGroupId, StudentId) bypassing service
            var e2 = Enrollment.Create(orgId, groupId, studentId, 0m, EnrollmentConstants.Statuses.Active);
            context.Enrollments.Add(e2);

            var act = () => context.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>("SQL Server index IX_Enrollments_Group_Student enforces unique (CourseGroupId, StudentId).");
        }
    }

    [Fact]
    public async Task TransactionRollback_WhenFailureOccursMidTransaction_LeavesNoOrphanRecords()
    {
        var orgId = Guid.NewGuid();
        var initialCount = 0;

        using (var setupContext = _fixture.CreateNewDbContext(orgId))
        {
            var org = new Organization
            {
                Id = orgId,
                Name = $"Rollback Org {Guid.NewGuid():N}",
                Slug = $"rb-org-{Guid.NewGuid():N}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            setupContext.Organizations.Add(org);
            await setupContext.SaveChangesAsync();

            initialCount = await setupContext.Enrollments.CountAsync();
        }

        // Attempt enrollment for non-existent CourseGroup - fails within validation/transaction
        using (var scope = _fixture.CreateScope(orgId))
        {
            var service = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
            var nonExistentGroupId = Guid.NewGuid();
            var dummyStudentId = Guid.NewGuid();

            var act = () => service.CreateAsync(new CreateEnrollmentDto
            {
                CourseGroupId = nonExistentGroupId,
                StudentId = dummyStudentId
            });

            await act.Should().ThrowAsync<Exception>();
        }

        // Verify that database state was completely rolled back and no orphan rows were added
        using (var verifyContext = _fixture.CreateNewDbContext(orgId))
        {
            var countAfter = await verifyContext.Enrollments.CountAsync();
            countAfter.Should().Be(initialCount, "Transaction rollback must ensure zero uncommitted or orphan records are persisted.");
        }
    }
}
