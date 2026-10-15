using System.Collections.Concurrent;
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

namespace TrainHub.IntegrationTests.Concurrency;

[Collection("SqlServer")]
public class SqlServerCapacityConcurrencyTests : IClassFixture<SqlServerTestFixture>
{
    private readonly SqlServerTestFixture _fixture;

    public SqlServerCapacityConcurrencyTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConcurrentEnrollments_AcrossIndependentDbContexts_NeverExceedsCapacityInvariant()
    {
        // ── Arrange: Create isolated test organization, course, and a group with strict capacity ──
        var orgId = Guid.NewGuid();
        var orgSlug = $"test-org-{Guid.NewGuid():N}";
        const int capacity = 3;
        const int totalContenders = 10;

        using (var setupContext = _fixture.CreateNewDbContext(orgId))
        {
            var org = new Organization
            {
                Id = orgId,
                Name = $"Capacity Org {orgSlug}",
                Slug = orgSlug,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            setupContext.Organizations.Add(org);

            var course = Course.Create(orgId, "Concurrent Scalability Test Course");
            setupContext.Courses.Add(course);

            var courseGroup = CourseGroup.Create(
                orgId,
                course.Id,
                "Cohort Alpha",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                capacity: capacity);
            setupContext.CourseGroups.Add(courseGroup);

            // Create 10 distinct students
            for (var i = 1; i <= totalContenders; i++)
            {
                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    FirstName = "Contender",
                    LastName = $"Student{i}",
                    Email = $"student{i}_{orgSlug}@test.com",
                    UserName = $"student{i}_{orgSlug}@test.com",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                setupContext.ApplicationUsers.Add(user);

                var student = Student.Create(
                    user.Id,
                    orgId,
                    $"STU-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
                    guardianPhone: "1234567890",
                    academicYear: "2026",
                    dateOfBirth: null,
                    notes: "Load testing contender");
                setupContext.Students.Add(student);
            }

            await setupContext.SaveChangesAsync();
        }

        // Fetch students and course group IDs
        List<Guid> studentIds;
        Guid groupId;
        using (var queryContext = _fixture.CreateNewDbContext(orgId))
        {
            var group = await queryContext.CourseGroups.FirstAsync(cg => cg.OrganizationId == orgId);
            groupId = group.Id;
            studentIds = await queryContext.Students
                .Where(s => s.OrganizationId == orgId)
                .Select(s => s.Id)
                .ToListAsync();
        }

        studentIds.Count.Should().Be(totalContenders);

        // ── Act: Launch 10 simultaneous enrollment requests, each on an independent DbContext scope ──
        // This precisely simulates separate multi-instance backend web servers executing concurrently against SQL Server.
        var barrier = new Barrier(totalContenders);
        var successList = new ConcurrentBag<EnrollmentDto>();
        var conflictList = new ConcurrentBag<ConflictException>();
        var otherErrors = new ConcurrentBag<Exception>();

        var tasks = studentIds.Select(async studentId =>
        {
            // Create an isolated service scope (simulating an independent web request on a separate backend server)
            using var scope = _fixture.CreateScope(orgId);
            var enrollmentService = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();

            var dto = new CreateEnrollmentDto
            {
                CourseGroupId = groupId,
                StudentId = studentId,
                PaidAmount = 100m,
                Status = EnrollmentConstants.Statuses.Active,
                EnrollmentDate = DateTime.UtcNow
            };

            // Synchronize all tasks to hit the database at the exact same millisecond
            await Task.Yield();
            barrier.SignalAndWait();

            try
            {
                var result = await enrollmentService.CreateAsync(dto);
                successList.Add(result);
            }
            catch (ConflictException cex)
            {
                conflictList.Add(cex);
            }
            catch (Exception ex)
            {
                otherErrors.Add(ex);
            }
        });

        await Task.WhenAll(tasks);

        // ── Assert: Invariant ActiveEnrollments <= Capacity MUST strictly hold ──
        otherErrors.Should().BeEmpty("No unexpected errors should occur during concurrent enrollment.");
        successList.Count.Should().Be(capacity, $"Exactly {capacity} enrollments should succeed because group capacity is {capacity}.");
        conflictList.Count.Should().Be(totalContenders - capacity, $"Remaining {totalContenders - capacity} requests should be safely rejected with ConflictException.");

        // Verify authoritative database state in SQL Server
        using (var verifyContext = _fixture.CreateNewDbContext(orgId))
        {
            var activeCountInDb = await verifyContext.Enrollments
                .IgnoreQueryFilters()
                .CountAsync(e => e.CourseGroupId == groupId && e.Status != EnrollmentConstants.Statuses.Cancelled);

            activeCountInDb.Should().Be(capacity);
            activeCountInDb.Should().BeLessThanOrEqualTo(capacity);
        }
    }

    [Fact]
    public async Task ConcurrentReactivations_CompeteForLastAvailableSeat_NeverExceedsCapacityInvariant()
    {
        // ── Arrange: Create a group with capacity 2: 1 Active enrollment and 2 Cancelled enrollments ──
        var orgId = Guid.NewGuid();
        var orgSlug = $"reactivate-org-{Guid.NewGuid():N}";
        const int capacity = 2;

        Guid groupId;
        Guid cancelledEnrollment1Id;
        Guid cancelledEnrollment2Id;

        using (var setupContext = _fixture.CreateNewDbContext(orgId))
        {
            var org = new Organization
            {
                Id = orgId,
                Name = $"Reactivation Org {orgSlug}",
                Slug = orgSlug,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            setupContext.Organizations.Add(org);

            var course = Course.Create(orgId, "Reactivation Test Course");
            setupContext.Courses.Add(course);

            var courseGroup = CourseGroup.Create(
                orgId,
                course.Id,
                "Cohort Reactivate",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                capacity: capacity);
            setupContext.CourseGroups.Add(courseGroup);
            groupId = courseGroup.Id;

            // Student 1 (Active)
            var u1 = new ApplicationUser { Id = Guid.NewGuid(), OrganizationId = orgId, FirstName = "S1", LastName = "User", Email = $"s1_{orgSlug}@test.com", UserName = $"s1_{orgSlug}@test.com", IsActive = true };
            var s1 = Student.Create(u1.Id, orgId, "STU-001");
            var e1 = Enrollment.Create(orgId, groupId, s1.Id, 100m, EnrollmentConstants.Statuses.Active);

            // Student 2 (Cancelled)
            var u2 = new ApplicationUser { Id = Guid.NewGuid(), OrganizationId = orgId, FirstName = "S2", LastName = "User", Email = $"s2_{orgSlug}@test.com", UserName = $"s2_{orgSlug}@test.com", IsActive = true };
            var s2 = Student.Create(u2.Id, orgId, "STU-002");
            var e2 = Enrollment.Create(orgId, groupId, s2.Id, 100m, EnrollmentConstants.Statuses.Cancelled);

            // Student 3 (Cancelled)
            var u3 = new ApplicationUser { Id = Guid.NewGuid(), OrganizationId = orgId, FirstName = "S3", LastName = "User", Email = $"s3_{orgSlug}@test.com", UserName = $"s3_{orgSlug}@test.com", IsActive = true };
            var s3 = Student.Create(u3.Id, orgId, "STU-003");
            var e3 = Enrollment.Create(orgId, groupId, s3.Id, 100m, EnrollmentConstants.Statuses.Cancelled);

            setupContext.ApplicationUsers.AddRange(u1, u2, u3);
            setupContext.Students.AddRange(s1, s2, s3);
            setupContext.Enrollments.AddRange(e1, e2, e3);

            await setupContext.SaveChangesAsync();

            cancelledEnrollment1Id = e2.Id;
            cancelledEnrollment2Id = e3.Id;
        }

        // ── Act: Concurrently attempt to reactivate both cancelled enrollments on separate DbContext scopes ──
        // Only 1 seat is available (capacity 2 - 1 active = 1 free seat). Exactly 1 must succeed and 1 must fail.
        var barrier = new Barrier(2);
        var successList = new ConcurrentBag<EnrollmentDto>();
        var conflictList = new ConcurrentBag<ConflictException>();
        var otherErrors = new ConcurrentBag<Exception>();

        var task1 = Task.Run(async () =>
        {
            using var scope = _fixture.CreateScope(orgId);
            var service = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
            barrier.SignalAndWait();
            try
            {
                var res = await service.UpdateStatusAsync(cancelledEnrollment1Id, new UpdateEnrollmentStatusDto { Status = EnrollmentConstants.Statuses.Active });
                successList.Add(res);
            }
            catch (ConflictException cex) { conflictList.Add(cex); }
            catch (Exception ex) { otherErrors.Add(ex); }
        });

        var task2 = Task.Run(async () =>
        {
            using var scope = _fixture.CreateScope(orgId);
            var service = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
            barrier.SignalAndWait();
            try
            {
                var res = await service.UpdateStatusAsync(cancelledEnrollment2Id, new UpdateEnrollmentStatusDto { Status = EnrollmentConstants.Statuses.Active });
                successList.Add(res);
            }
            catch (ConflictException cex) { conflictList.Add(cex); }
            catch (Exception ex) { otherErrors.Add(ex); }
        });

        await Task.WhenAll(task1, task2);

        // ── Assert: Invariant ActiveEnrollments <= Capacity holds ──
        otherErrors.Should().BeEmpty();
        successList.Count.Should().Be(1, "Only 1 reactivation should succeed because only 1 seat was available.");
        conflictList.Count.Should().Be(1, "The competing reactivation must be rejected due to capacity.");

        using (var verifyContext = _fixture.CreateNewDbContext(orgId))
        {
            var activeCount = await verifyContext.Enrollments
                .IgnoreQueryFilters()
                .CountAsync(e => e.CourseGroupId == groupId && e.Status != EnrollmentConstants.Statuses.Cancelled);

            activeCount.Should().Be(2);
            activeCount.Should().BeLessThanOrEqualTo(capacity);
        }
    }
}
