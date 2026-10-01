using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;

namespace TrainHub.UnitTests.Infrastructure;

public class TrainHubDbContextTenantIsolationTests
{
    [Fact]
    public async Task SaveChangesAsync_WhenTenantContextActive_StampsTenantIdOnNewEntities()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var currentOrgMock = new Mock<ICurrentOrganizationService>();
        currentOrgMock.Setup(s => s.OrganizationId).Returns(tenantId);

        var options = new DbContextOptionsBuilder<TrainHubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new TrainHubDbContext(options, currentOrgMock.Object);

        var course = Course.Create(tenantId, "Tenant Course");

        // Act
        dbContext.Courses.Add(course);
        await dbContext.SaveChangesAsync();

        // Assert
        course.OrganizationId.Should().Be(tenantId);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenCrossTenantEntityAdded_ThrowsInvalidOperationException()
    {
        // Arrange
        var activeTenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var currentOrgMock = new Mock<ICurrentOrganizationService>();
        currentOrgMock.Setup(s => s.OrganizationId).Returns(activeTenantId);

        var options = new DbContextOptionsBuilder<TrainHubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new TrainHubDbContext(options, currentOrgMock.Object);

        // Attempting to add a course belonging to another tenant
        var crossTenantCourse = Course.Create(foreignTenantId, "Cross Tenant Course");
        dbContext.Courses.Add(crossTenantCourse);

        // Act
        var act = () => dbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cross-tenant operation detected*");
    }

    [Fact]
    public async Task Query_WhenTenantContextActive_FiltersOutOtherTenants()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<TrainHubDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        // Seed courses for tenant A and tenant B without tenant filter
        using (var seedContext = new TrainHubDbContext(options))
        {
            seedContext.Courses.AddRange(
                Course.Create(tenantA, "Course Tenant A"),
                Course.Create(tenantB, "Course Tenant B")
            );
            await seedContext.SaveChangesAsync();
        }

        // Query with tenant A context
        var currentOrgMock = new Mock<ICurrentOrganizationService>();
        currentOrgMock.Setup(s => s.OrganizationId).Returns(tenantA);

        using var tenantAContext = new TrainHubDbContext(options, currentOrgMock.Object);

        // Act
        var visibleCourses = await tenantAContext.Courses.ToListAsync();

        // Assert
        visibleCourses.Should().ContainSingle();
        visibleCourses[0].Name.Should().Be("Course Tenant A");
        visibleCourses[0].OrganizationId.Should().Be(tenantA);
    }
}
