using FluentAssertions;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Enums;
using TrainHub.Domain.Models;

namespace TrainHub.UnitTests.Domain;

public class EnrollmentTests
{
    private readonly Guid _validOrgId = Guid.NewGuid();
    private readonly Guid _validCourseGroupId = Guid.NewGuid();
    private readonly Guid _validStudentId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ReturnsEnrollmentInstance()
    {
        // Arrange
        var customDate = new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var enrollment = Enrollment.Create(
            _validOrgId,
            _validCourseGroupId,
            _validStudentId,
            paidAmount: 250m,
            status: EnrollmentConstants.Statuses.Active,
            enrollmentDate: customDate);

        // Assert
        enrollment.Should().NotBeNull();
        enrollment.Id.Should().NotBeEmpty();
        enrollment.OrganizationId.Should().Be(_validOrgId);
        enrollment.CourseGroupId.Should().Be(_validCourseGroupId);
        enrollment.StudentId.Should().Be(_validStudentId);
        enrollment.PaidAmount.Should().Be(250m);
        enrollment.Status.Should().Be(EnrollmentConstants.Statuses.Active);
        enrollment.EnrollmentStatus.Should().Be(EnrollmentStatus.Active);
        enrollment.EnrollmentDate.Should().Be(customDate);
        enrollment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        enrollment.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithDefaults_SetsExpectedDefaults()
    {
        // Act
        var enrollment = Enrollment.Create(_validOrgId, _validCourseGroupId, _validStudentId);

        // Assert
        enrollment.OrganizationId.Should().Be(_validOrgId);
        enrollment.CourseGroupId.Should().Be(_validCourseGroupId);
        enrollment.StudentId.Should().Be(_validStudentId);
        enrollment.PaidAmount.Should().Be(0m);
        enrollment.Status.Should().Be(EnrollmentConstants.DefaultStatus);
        enrollment.EnrollmentStatus.Should().Be(EnrollmentStatus.Active);
        enrollment.EnrollmentDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithEmptyOrganizationId_ThrowsArgumentException()
    {
        // Act
        var act = () => Enrollment.Create(Guid.Empty, _validCourseGroupId, _validStudentId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*OrganizationId cannot be empty*")
            .WithParameterName("organizationId");
    }

    [Fact]
    public void Create_WithEmptyCourseGroupId_ThrowsArgumentException()
    {
        // Act
        var act = () => Enrollment.Create(_validOrgId, Guid.Empty, _validStudentId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*CourseGroupId cannot be empty*")
            .WithParameterName("courseGroupId");
    }

    [Fact]
    public void Create_WithEmptyStudentId_ThrowsArgumentException()
    {
        // Act
        var act = () => Enrollment.Create(_validOrgId, _validCourseGroupId, Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*StudentId cannot be empty*")
            .WithParameterName("studentId");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithNegativePaidAmount_ThrowsArgumentException(decimal invalidAmount)
    {
        // Act
        var act = () => Enrollment.Create(_validOrgId, _validCourseGroupId, _validStudentId, paidAmount: invalidAmount);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*PaidAmount must be between*");
    }

    [Fact]
    public void Create_WithPaidAmountExceedingMax_ThrowsArgumentException()
    {
        // Act
        var act = () => Enrollment.Create(_validOrgId, _validCourseGroupId, _validStudentId, paidAmount: 1_000_001m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*PaidAmount must be between*");
    }

    [Fact]
    public void Create_WithInvalidStatus_ThrowsArgumentException()
    {
        // Act
        var act = () => Enrollment.Create(_validOrgId, _validCourseGroupId, _validStudentId, status: 99);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Status '99' is invalid*");
    }

    [Fact]
    public void UpdateDetails_WithValidValues_UpdatesPropertiesAndTimestamp()
    {
        // Arrange
        var enrollment = Enrollment.Create(_validOrgId, _validCourseGroupId, _validStudentId, paidAmount: 50m, status: EnrollmentConstants.Statuses.Pending);

        // Act
        enrollment.UpdateDetails(paidAmount: 200m, status: EnrollmentConstants.Statuses.Active);

        // Assert
        enrollment.PaidAmount.Should().Be(200m);
        enrollment.Status.Should().Be(EnrollmentConstants.Statuses.Active);
        enrollment.EnrollmentStatus.Should().Be(EnrollmentStatus.Active);
    }

    [Fact]
    public void UpdatePaidAmount_WithValidAmount_UpdatesPaidAmount()
    {
        // Arrange
        var enrollment = Enrollment.Create(_validOrgId, _validCourseGroupId, _validStudentId, paidAmount: 50m);

        // Act
        enrollment.UpdatePaidAmount(150m);

        // Assert
        enrollment.PaidAmount.Should().Be(150m);
    }

    [Fact]
    public void UpdatePaidAmount_WithNegativeAmount_ThrowsArgumentException()
    {
        // Arrange
        var enrollment = Enrollment.Create(_validOrgId, _validCourseGroupId, _validStudentId);

        // Act
        var act = () => enrollment.UpdatePaidAmount(-10m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*PaidAmount must be between*");
    }

    [Fact]
    public void UpdateStatus_WithValidByte_UpdatesStatus()
    {
        // Arrange
        var enrollment = Enrollment.Create(_validOrgId, _validCourseGroupId, _validStudentId);

        // Act
        enrollment.UpdateStatus(EnrollmentConstants.Statuses.Completed);

        // Assert
        enrollment.Status.Should().Be(EnrollmentConstants.Statuses.Completed);
        enrollment.EnrollmentStatus.Should().Be(EnrollmentStatus.Completed);
    }

    [Fact]
    public void UpdateStatus_WithValidEnum_UpdatesStatus()
    {
        // Arrange
        var enrollment = Enrollment.Create(_validOrgId, _validCourseGroupId, _validStudentId);

        // Act
        enrollment.UpdateStatus(EnrollmentStatus.Cancelled);

        // Assert
        enrollment.Status.Should().Be(EnrollmentConstants.Statuses.Cancelled);
        enrollment.EnrollmentStatus.Should().Be(EnrollmentStatus.Cancelled);
    }

    [Fact]
    public void UpdateStatus_WithInvalidStatus_ThrowsArgumentException()
    {
        // Arrange
        var enrollment = Enrollment.Create(_validOrgId, _validCourseGroupId, _validStudentId);

        // Act
        var act = () => enrollment.UpdateStatus(250);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Status '250' is invalid*");
    }

    [Fact]
    public void Activate_SetsStatusToActive()
    {
        // Arrange
        var enrollment = Enrollment.Create(_validOrgId, _validCourseGroupId, _validStudentId, status: EnrollmentConstants.Statuses.Pending);

        // Act
        enrollment.Activate();

        // Assert
        enrollment.Status.Should().Be(EnrollmentConstants.Statuses.Active);
        enrollment.EnrollmentStatus.Should().Be(EnrollmentStatus.Active);
    }

    [Fact]
    public void Complete_SetsStatusToCompleted()
    {
        // Arrange
        var enrollment = Enrollment.Create(_validOrgId, _validCourseGroupId, _validStudentId);

        // Act
        enrollment.Complete();

        // Assert
        enrollment.Status.Should().Be(EnrollmentConstants.Statuses.Completed);
        enrollment.EnrollmentStatus.Should().Be(EnrollmentStatus.Completed);
    }

    [Fact]
    public void Cancel_SetsStatusToCancelled()
    {
        // Arrange
        var enrollment = Enrollment.Create(_validOrgId, _validCourseGroupId, _validStudentId);

        // Act
        enrollment.Cancel();

        // Assert
        enrollment.Status.Should().Be(EnrollmentConstants.Statuses.Cancelled);
        enrollment.EnrollmentStatus.Should().Be(EnrollmentStatus.Cancelled);
    }
}
