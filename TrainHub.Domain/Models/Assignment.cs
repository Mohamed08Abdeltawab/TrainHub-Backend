using TrainHub.Domain.Constants;

namespace TrainHub.Domain.Models;

/// <summary>
/// Represents an Assignment created for a CourseGroup (cohort) within an Organization.
/// </summary>
public partial class Assignment
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid CourseGroupId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTime DueDate { get; private set; }

    public int MaxScore { get; private set; } = AssignmentConstants.DefaultMaxScore;

    public byte Status { get; private set; } = AssignmentConstants.DefaultStatus;

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public virtual CourseGroup CourseGroup { get; private set; } = null!;

    public virtual Organization Organization { get; private set; } = null!;

    protected Assignment()
    {
    }

    public static Assignment Create(
        Guid organizationId,
        Guid courseGroupId,
        string title,
        DateTime dueDate,
        string? description = null,
        int maxScore = AssignmentConstants.DefaultMaxScore,
        byte status = AssignmentConstants.DefaultStatus,
        Guid? id = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));

        if (courseGroupId == Guid.Empty)
            throw new ArgumentException("CourseGroupId cannot be empty.", nameof(courseGroupId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        var trimmedTitle = title.Trim();
        if (trimmedTitle.Length < AssignmentConstants.TitleMinLength || trimmedTitle.Length > AssignmentConstants.TitleMaxLength)
            throw new ArgumentException($"Title must be between {AssignmentConstants.TitleMinLength} and {AssignmentConstants.TitleMaxLength} characters.", nameof(title));

        if (maxScore < AssignmentConstants.MinScore || maxScore > AssignmentConstants.MaxScore)
            throw new ArgumentException($"MaxScore must be between {AssignmentConstants.MinScore} and {AssignmentConstants.MaxScore}.", nameof(maxScore));

        if (!AssignmentConstants.Statuses.IsValid(status))
            throw new ArgumentException($"Invalid assignment status '{status}'.", nameof(status));

        var now = DateTime.UtcNow;

        return new Assignment
        {
            Id = id ?? Guid.NewGuid(),
            OrganizationId = organizationId,
            CourseGroupId = courseGroupId,
            Title = trimmedTitle,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DueDate = dueDate,
            MaxScore = maxScore,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void UpdateDetails(
        string title,
        DateTime dueDate,
        string? description = null,
        int? maxScore = null,
        byte? status = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        var trimmedTitle = title.Trim();
        if (trimmedTitle.Length < AssignmentConstants.TitleMinLength || trimmedTitle.Length > AssignmentConstants.TitleMaxLength)
            throw new ArgumentException($"Title must be between {AssignmentConstants.TitleMinLength} and {AssignmentConstants.TitleMaxLength} characters.", nameof(title));

        if (maxScore.HasValue)
        {
            if (maxScore.Value < AssignmentConstants.MinScore || maxScore.Value > AssignmentConstants.MaxScore)
                throw new ArgumentException($"MaxScore must be between {AssignmentConstants.MinScore} and {AssignmentConstants.MaxScore}.", nameof(maxScore));
            MaxScore = maxScore.Value;
        }

        if (status.HasValue)
        {
            if (!AssignmentConstants.Statuses.IsValid(status.Value))
                throw new ArgumentException($"Invalid assignment status '{status.Value}'.", nameof(status));
            Status = status.Value;
        }

        Title = trimmedTitle;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DueDate = dueDate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(byte newStatus)
    {
        if (!AssignmentConstants.Statuses.IsValid(newStatus))
            throw new ArgumentException($"Invalid assignment status '{newStatus}'.", nameof(newStatus));

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }
}
