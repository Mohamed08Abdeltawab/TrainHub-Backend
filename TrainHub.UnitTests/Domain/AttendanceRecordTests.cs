using FluentAssertions;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Enums;
using TrainHub.Domain.Models;

namespace TrainHub.UnitTests.Domain;

public class AttendanceRecordTests
{
    private readonly Guid _validOrgId = Guid.NewGuid();
    private readonly Guid _validSessionId = Guid.NewGuid();
    private readonly Guid _validStudentId = Guid.NewGuid();
    private readonly Guid _validUserId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ReturnsAttendanceRecordInstance()
    {
        // Arrange
        var customDate = new DateTime(2026, 10, 5, 14, 0, 0, DateTimeKind.Utc);

        // Act
        var record = AttendanceRecord.Create(
            _validOrgId,
            _validSessionId,
            _validStudentId,
            status: AttendanceConstants.Statuses.Present,
            markedByUserId: _validUserId,
            notes: "Attended on time",
            markedAt: customDate);

        // Assert
        record.Should().NotBeNull();
        record.Id.Should().NotBeEmpty();
        record.OrganizationId.Should().Be(_validOrgId);
        record.SessionId.Should().Be(_validSessionId);
        record.StudentId.Should().Be(_validStudentId);
        record.MarkedByUserId.Should().Be(_validUserId);
        record.Status.Should().Be(AttendanceConstants.Statuses.Present);
        record.AttendanceStatus.Should().Be(AttendanceStatus.Present);
        record.Notes.Should().Be("Attended on time");
        record.MarkedAt.Should().Be(customDate);
    }

    [Fact]
    public void Create_WithDefaults_SetsExpectedDefaults()
    {
        // Act
        var record = AttendanceRecord.Create(
            _validOrgId,
            _validSessionId,
            _validStudentId);

        // Assert
        record.OrganizationId.Should().Be(_validOrgId);
        record.SessionId.Should().Be(_validSessionId);
        record.StudentId.Should().Be(_validStudentId);
        record.MarkedByUserId.Should().BeNull();
        record.Status.Should().Be(AttendanceConstants.DefaultStatus);
        record.AttendanceStatus.Should().Be(AttendanceStatus.Present);
        record.Notes.Should().BeNull();
        record.MarkedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithEmptyOrganizationId_ThrowsArgumentException()
    {
        // Act
        var act = () => AttendanceRecord.Create(
            Guid.Empty,
            _validSessionId,
            _validStudentId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("organizationId")
            .WithMessage("*OrganizationId cannot be empty.*");
    }

    [Fact]
    public void Create_WithEmptySessionId_ThrowsArgumentException()
    {
        // Act
        var act = () => AttendanceRecord.Create(
            _validOrgId,
            Guid.Empty,
            _validStudentId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("sessionId")
            .WithMessage("*SessionId cannot be empty.*");
    }

    [Fact]
    public void Create_WithEmptyStudentId_ThrowsArgumentException()
    {
        // Act
        var act = () => AttendanceRecord.Create(
            _validOrgId,
            _validSessionId,
            Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("studentId")
            .WithMessage("*StudentId cannot be empty.*");
    }

    [Fact]
    public void Create_WithEmptyMarkedByUserId_ThrowsArgumentException()
    {
        // Act
        var act = () => AttendanceRecord.Create(
            _validOrgId,
            _validSessionId,
            _validStudentId,
            markedByUserId: Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("markedByUserId")
            .WithMessage("*MarkedByUserId cannot be empty GUID.*");
    }

    [Fact]
    public void Create_WithInvalidStatus_ThrowsArgumentException()
    {
        // Act
        var act = () => AttendanceRecord.Create(
            _validOrgId,
            _validSessionId,
            _validStudentId,
            status: 99);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("status")
            .WithMessage("*Status '99' is invalid.*");
    }

    [Fact]
    public void Create_WithExcessivelyLongNotes_ThrowsArgumentException()
    {
        // Arrange
        var longNotes = new string('A', AttendanceConstants.NotesMaxLength + 1);

        // Act
        var act = () => AttendanceRecord.Create(
            _validOrgId,
            _validSessionId,
            _validStudentId,
            notes: longNotes);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("notes")
            .WithMessage("*Notes exceeds maximum length*");
    }

    [Fact]
    public void Create_TrimsWhitespaceFromNotes()
    {
        // Act
        var record = AttendanceRecord.Create(
            _validOrgId,
            _validSessionId,
            _validStudentId,
            notes: "  Student was late   ");

        // Assert
        record.Notes.Should().Be("Student was late");
    }

    [Fact]
    public void Create_WithWhitespaceOnlyNotes_NormalizesToNull()
    {
        // Act
        var record = AttendanceRecord.Create(
            _validOrgId,
            _validSessionId,
            _validStudentId,
            notes: "    ");

        // Assert
        record.Notes.Should().BeNull();
    }

    [Fact]
    public void UpdateDetails_UpdatesStatusNotesAndMarkedByUser()
    {
        // Arrange
        var record = AttendanceRecord.Create(_validOrgId, _validSessionId, _validStudentId);
        var newUserId = Guid.NewGuid();

        // Act
        record.UpdateDetails(
            status: AttendanceConstants.Statuses.Late,
            notes: "Arrived 15 minutes late",
            markedByUserId: newUserId);

        // Assert
        record.Status.Should().Be(AttendanceConstants.Statuses.Late);
        record.AttendanceStatus.Should().Be(AttendanceStatus.Late);
        record.Notes.Should().Be("Arrived 15 minutes late");
        record.MarkedByUserId.Should().Be(newUserId);
        record.MarkedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void UpdateStatus_WithValidStatus_UpdatesStatusAndTimestamp()
    {
        // Arrange
        var record = AttendanceRecord.Create(_validOrgId, _validSessionId, _validStudentId);

        // Act
        record.UpdateStatus(AttendanceConstants.Statuses.Absent);

        // Assert
        record.Status.Should().Be(AttendanceConstants.Statuses.Absent);
        record.AttendanceStatus.Should().Be(AttendanceStatus.Absent);
        record.MarkedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void UpdateStatus_WithInvalidStatus_ThrowsArgumentException()
    {
        // Arrange
        var record = AttendanceRecord.Create(_validOrgId, _validSessionId, _validStudentId);

        // Act
        var act = () => record.UpdateStatus(200);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newStatus")
            .WithMessage("*Status '200' is invalid.*");
    }

    [Fact]
    public void StatusHelperMethods_UpdateToExpectedStatuses()
    {
        // Arrange
        var record = AttendanceRecord.Create(_validOrgId, _validSessionId, _validStudentId);

        // Act & Assert
        record.MarkAbsent();
        record.Status.Should().Be(AttendanceConstants.Statuses.Absent);
        record.AttendanceStatus.Should().Be(AttendanceStatus.Absent);

        record.MarkPresent();
        record.Status.Should().Be(AttendanceConstants.Statuses.Present);
        record.AttendanceStatus.Should().Be(AttendanceStatus.Present);

        record.MarkLate();
        record.Status.Should().Be(AttendanceConstants.Statuses.Late);
        record.AttendanceStatus.Should().Be(AttendanceStatus.Late);

        record.MarkExcused();
        record.Status.Should().Be(AttendanceConstants.Statuses.Excused);
        record.AttendanceStatus.Should().Be(AttendanceStatus.Excused);
    }

    [Fact]
    public void UpdateNotes_UpdatesAndNormalizesNotes()
    {
        // Arrange
        var record = AttendanceRecord.Create(_validOrgId, _validSessionId, _validStudentId);

        // Act
        record.UpdateNotes("  Doctor excuse submitted  ");

        // Assert
        record.Notes.Should().Be("Doctor excuse submitted");
    }

    [Theory]
    [InlineData(AttendanceConstants.Statuses.Present, true)]
    [InlineData(AttendanceConstants.Statuses.Late, true)]
    [InlineData(AttendanceConstants.Statuses.Absent, false)]
    [InlineData(AttendanceConstants.Statuses.Excused, false)]
    public void IsAttended_ReturnsExpectedValueForStatus(byte status, bool expected)
    {
        // Arrange
        var record = AttendanceRecord.Create(_validOrgId, _validSessionId, _validStudentId, status: status);

        // Act & Assert
        record.IsAttended.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 0, 0.0)]
    [InlineData(0, 5, 0.0)]
    [InlineData(5, 0, 0.0)]
    [InlineData(5, 5, 100.0)]
    [InlineData(9, 10, 90.0)]
    [InlineData(1, 3, 33.33)]
    [InlineData(2, 3, 66.67)]
    public void CalculatePercentage_ReturnsCorrectRoundedPercentage(int attended, int total, decimal expected)
    {
        // Act
        var result = AttendanceConstants.CalculatePercentage(attended, total);

        // Assert
        result.Should().Be(expected);
    }
}
