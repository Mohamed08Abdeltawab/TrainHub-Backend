using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Dashboard.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.Application.Services;

/// <summary>
/// Service implementation for querying dashboard statistics across students, courses, instructors, and attendance.
/// Enforces multi-tenancy and organization scoping for all calculations.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly ITrainHubDbContext _dbContext;
    private readonly ICurrentOrganizationService _currentOrganizationService;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        ITrainHubDbContext dbContext,
        ICurrentOrganizationService currentOrganizationService,
        ILogger<DashboardService> logger)
    {
        _dbContext = dbContext;
        _currentOrganizationService = currentOrganizationService;
        _logger = logger;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var organizationId = GetRequiredOrganizationId();

        _logger.LogInformation("Retrieving comprehensive dashboard summary for organization '{OrganizationId}'.", organizationId);

        var students = await GetStudentStatisticsAsync(cancellationToken);
        var courses = await GetCourseStatisticsAsync(cancellationToken);
        var instructors = await GetInstructorStatisticsAsync(cancellationToken);
        var attendance = await GetAttendanceStatisticsAsync(null, cancellationToken);

        return new DashboardSummaryDto
        {
            OrganizationId = organizationId,
            Students = students,
            Courses = courses,
            Instructors = instructors,
            Attendance = attendance
        };
    }

    public async Task<StudentStatisticsDto> GetStudentStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var organizationId = GetRequiredOrganizationId();

        _logger.LogInformation("Retrieving student statistics for organization '{OrganizationId}'.", organizationId);

        var totalStudents = await _dbContext.Students
            .AsNoTracking()
            .Where(s => s.OrganizationId == organizationId)
            .CountAsync(cancellationToken);

        var activeStudents = await _dbContext.Students
            .AsNoTracking()
            .Where(s => s.OrganizationId == organizationId && s.User.IsActive)
            .CountAsync(cancellationToken);

        var inactiveStudents = totalStudents - activeStudents;

        var enrollmentCounts = await _dbContext.Enrollments
            .AsNoTracking()
            .Where(e => e.OrganizationId == organizationId)
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalEnrollments = enrollmentCounts.Sum(x => x.Count);
        var activeEnrollments = enrollmentCounts.FirstOrDefault(x => x.Status == EnrollmentConstants.Statuses.Active)?.Count ?? 0;
        var completedEnrollments = enrollmentCounts.FirstOrDefault(x => x.Status == EnrollmentConstants.Statuses.Completed)?.Count ?? 0;
        var cancelledEnrollments = enrollmentCounts.FirstOrDefault(x => x.Status == EnrollmentConstants.Statuses.Cancelled)?.Count ?? 0;
        var pendingEnrollments = enrollmentCounts.FirstOrDefault(x => x.Status == EnrollmentConstants.Statuses.Pending)?.Count ?? 0;

        var studentsWithActiveEnrollments = await _dbContext.Enrollments
            .AsNoTracking()
            .Where(e => e.OrganizationId == organizationId && e.Status == EnrollmentConstants.Statuses.Active)
            .Select(e => e.StudentId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new StudentStatisticsDto
        {
            TotalStudents = totalStudents,
            ActiveStudents = activeStudents,
            InactiveStudents = inactiveStudents,
            TotalEnrollments = totalEnrollments,
            ActiveEnrollments = activeEnrollments,
            CompletedEnrollments = completedEnrollments,
            CancelledEnrollments = cancelledEnrollments,
            PendingEnrollments = pendingEnrollments,
            StudentsWithActiveEnrollments = studentsWithActiveEnrollments
        };
    }

    public async Task<CourseStatisticsDto> GetCourseStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var organizationId = GetRequiredOrganizationId();

        _logger.LogInformation("Retrieving course statistics for organization '{OrganizationId}'.", organizationId);

        var courseCounts = await _dbContext.Courses
            .AsNoTracking()
            .Where(c => c.OrganizationId == organizationId)
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalCourses = courseCounts.Sum(x => x.Count);
        var draftCourses = courseCounts.FirstOrDefault(x => x.Status == CourseConstants.Statuses.Draft)?.Count ?? 0;
        var activeCourses = courseCounts.FirstOrDefault(x => x.Status == CourseConstants.Statuses.Active)?.Count ?? 0;
        var inactiveCourses = courseCounts.FirstOrDefault(x => x.Status == CourseConstants.Statuses.Inactive)?.Count ?? 0;
        var archivedCourses = courseCounts.FirstOrDefault(x => x.Status == CourseConstants.Statuses.Archived)?.Count ?? 0;

        var groupCounts = await _dbContext.CourseGroups
            .AsNoTracking()
            .Where(cg => cg.OrganizationId == organizationId)
            .GroupBy(cg => cg.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalCourseGroups = groupCounts.Sum(x => x.Count);
        var draftCourseGroups = groupCounts.FirstOrDefault(x => x.Status == CourseGroupConstants.Statuses.Draft)?.Count ?? 0;
        var activeCourseGroups = groupCounts.FirstOrDefault(x => x.Status == CourseGroupConstants.Statuses.Active)?.Count ?? 0;
        var completedCourseGroups = groupCounts.FirstOrDefault(x => x.Status == CourseGroupConstants.Statuses.Completed)?.Count ?? 0;
        var cancelledCourseGroups = groupCounts.FirstOrDefault(x => x.Status == CourseGroupConstants.Statuses.Cancelled)?.Count ?? 0;

        var enrollmentCounts = await _dbContext.Enrollments
            .AsNoTracking()
            .Where(e => e.OrganizationId == organizationId)
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalEnrollments = enrollmentCounts.Sum(x => x.Count);
        var activeEnrollments = enrollmentCounts.FirstOrDefault(x => x.Status == EnrollmentConstants.Statuses.Active)?.Count ?? 0;

        return new CourseStatisticsDto
        {
            TotalCourses = totalCourses,
            ActiveCourses = activeCourses,
            InactiveCourses = inactiveCourses,
            DraftCourses = draftCourses,
            ArchivedCourses = archivedCourses,
            TotalCourseGroups = totalCourseGroups,
            ActiveCourseGroups = activeCourseGroups,
            CompletedCourseGroups = completedCourseGroups,
            CancelledCourseGroups = cancelledCourseGroups,
            DraftCourseGroups = draftCourseGroups,
            TotalEnrollments = totalEnrollments,
            ActiveEnrollments = activeEnrollments
        };
    }

    public async Task<InstructorStatisticsDto> GetInstructorStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var organizationId = GetRequiredOrganizationId();

        _logger.LogInformation("Retrieving instructor statistics for organization '{OrganizationId}'.", organizationId);

        var totalInstructors = await _dbContext.Instructors
            .AsNoTracking()
            .Where(i => i.OrganizationId == organizationId)
            .CountAsync(cancellationToken);

        var activeInstructors = await _dbContext.Instructors
            .AsNoTracking()
            .Where(i => i.OrganizationId == organizationId && i.User.IsActive)
            .CountAsync(cancellationToken);

        var inactiveInstructors = totalInstructors - activeInstructors;

        var assignedInstructors = await _dbContext.CourseGroups
            .AsNoTracking()
            .Where(cg => cg.OrganizationId == organizationId && cg.InstructorId.HasValue)
            .Select(cg => cg.InstructorId!.Value)
            .Distinct()
            .CountAsync(cancellationToken);

        var activeAssignedInstructors = await _dbContext.CourseGroups
            .AsNoTracking()
            .Where(cg => cg.OrganizationId == organizationId && cg.InstructorId.HasValue && cg.Status == CourseGroupConstants.Statuses.Active)
            .Select(cg => cg.InstructorId!.Value)
            .Distinct()
            .CountAsync(cancellationToken);

        return new InstructorStatisticsDto
        {
            TotalInstructors = totalInstructors,
            ActiveInstructors = activeInstructors,
            InactiveInstructors = inactiveInstructors,
            AssignedInstructors = assignedInstructors,
            ActiveAssignedInstructors = activeAssignedInstructors
        };
    }

    public async Task<AttendanceStatisticsDto> GetAttendanceStatisticsAsync(Guid? courseGroupId = null, CancellationToken cancellationToken = default)
    {
        var organizationId = GetRequiredOrganizationId();

        _logger.LogInformation("Retrieving attendance statistics for organization '{OrganizationId}' (Group: {CourseGroupId}).",
            organizationId, courseGroupId);

        if (courseGroupId.HasValue)
        {
            if (courseGroupId.Value == Guid.Empty)
            {
                throw new BadRequestException("CourseGroup ID cannot be empty.");
            }

            var groupExists = await _dbContext.CourseGroups
                .AsNoTracking()
                .AnyAsync(cg => cg.Id == courseGroupId.Value && cg.OrganizationId == organizationId, cancellationToken);

            if (!groupExists)
            {
                _logger.LogWarning("CourseGroup '{CourseGroupId}' not found in organization '{OrganizationId}'.",
                    courseGroupId.Value, organizationId);
                throw new NotFoundException("CourseGroup", courseGroupId.Value);
            }
        }

        var attendanceQuery = _dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.OrganizationId == organizationId);

        if (courseGroupId.HasValue)
        {
            attendanceQuery = attendanceQuery.Where(a => a.Session.CourseGroupId == courseGroupId.Value);
        }

        var attendanceCounts = await attendanceQuery
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalRecords = attendanceCounts.Sum(x => x.Count);
        var presentCount = attendanceCounts.FirstOrDefault(x => x.Status == AttendanceConstants.Statuses.Present)?.Count ?? 0;
        var lateCount = attendanceCounts.FirstOrDefault(x => x.Status == AttendanceConstants.Statuses.Late)?.Count ?? 0;
        var absentCount = attendanceCounts.FirstOrDefault(x => x.Status == AttendanceConstants.Statuses.Absent)?.Count ?? 0;
        var excusedCount = attendanceCounts.FirstOrDefault(x => x.Status == AttendanceConstants.Statuses.Excused)?.Count ?? 0;
        var attendedCount = presentCount + lateCount;

        var percentage = AttendanceConstants.CalculatePercentage(attendedCount, totalRecords);

        var sessionQuery = _dbContext.Sessions
            .AsNoTracking()
            .Where(s => s.OrganizationId == organizationId);

        if (courseGroupId.HasValue)
        {
            sessionQuery = sessionQuery.Where(s => s.CourseGroupId == courseGroupId.Value);
        }

        var sessionCounts = await sessionQuery
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalSessions = sessionCounts.Sum(x => x.Count);
        var draftSessions = sessionCounts.FirstOrDefault(x => x.Status == SessionConstants.Statuses.Draft)?.Count ?? 0;
        var scheduledSessions = sessionCounts.FirstOrDefault(x => x.Status == SessionConstants.Statuses.Scheduled)?.Count ?? 0;
        var completedSessions = sessionCounts.FirstOrDefault(x => x.Status == SessionConstants.Statuses.Completed)?.Count ?? 0;
        var cancelledSessions = sessionCounts.FirstOrDefault(x => x.Status == SessionConstants.Statuses.Cancelled)?.Count ?? 0;

        return new AttendanceStatisticsDto
        {
            CourseGroupId = courseGroupId,
            TotalRecords = totalRecords,
            AttendedCount = attendedCount,
            PresentCount = presentCount,
            LateCount = lateCount,
            AbsentCount = absentCount,
            ExcusedCount = excusedCount,
            AttendancePercentage = percentage,
            TotalSessions = totalSessions,
            ScheduledSessions = scheduledSessions,
            CompletedSessions = completedSessions,
            CancelledSessions = cancelledSessions,
            DraftSessions = draftSessions
        };
    }

    private Guid GetRequiredOrganizationId()
    {
        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Dashboard query failed: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        return organizationId.Value;
    }
}
