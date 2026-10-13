using System.Security.Claims;
using Microsoft.AspNetCore.Http;
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
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public DashboardService(
        ITrainHubDbContext dbContext,
        ICurrentOrganizationService currentOrganizationService,
        ILogger<DashboardService> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _dbContext = dbContext;
        _currentOrganizationService = currentOrganizationService;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
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

    public async Task<DashboardContentDto> GetDashboardContentAsync(CancellationToken cancellationToken = default)
    {
        var roles = GetCallerRoles();

        _logger.LogInformation("Resolving dashboard content for caller with roles: {Roles}.", string.Join(", ", roles));

        if (roles.Contains(AppRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase))
        {
            return await BuildSuperAdminAsync(cancellationToken);
        }

        if (roles.Contains(AppRoles.OrgAdmin, StringComparer.OrdinalIgnoreCase))
        {
            return await BuildOrgAdminAsync(cancellationToken);
        }

        if (roles.Contains(AppRoles.Instructor, StringComparer.OrdinalIgnoreCase))
        {
            return await BuildInstructorAsync(cancellationToken);
        }

        if (roles.Contains(AppRoles.Student, StringComparer.OrdinalIgnoreCase))
        {
            return await BuildStudentAsync(cancellationToken);
        }

        throw new ForbiddenAccessException("No dashboard is available for your role.");
    }

    private IReadOnlyList<string> GetCallerRoles()
    {
        var user = _httpContextAccessor?.HttpContext?.User;
        if (user == null) return [];

        return user.FindAll(ClaimTypes.Role)
            .Concat(user.FindAll("role"))
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Guid? GetCallerUserId()
    {
        var user = _httpContextAccessor?.HttpContext?.User;
        if (user == null) return null;

        var claim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
        if (claim != null && Guid.TryParse(claim.Value, out var id))
            return id;

        return null;
    }

    private async Task<DashboardContentDto> BuildSuperAdminAsync(CancellationToken ct)
    {
        var totalOrgs = await _dbContext.Organizations
            .IgnoreQueryFilters().AsNoTracking().CountAsync(ct);

        var totalUsers = await _dbContext.ApplicationUsers
            .IgnoreQueryFilters().AsNoTracking().CountAsync(ct);

        var totalCourses = await _dbContext.Courses
            .IgnoreQueryFilters().AsNoTracking().CountAsync(ct);

        var totalStudents = await _dbContext.Students
            .IgnoreQueryFilters().AsNoTracking().CountAsync(ct);

        var recentOrgs = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .ToListAsync(ct);

        var rows = recentOrgs.Select(o => new DashboardRowDto
        {
            Title = o.Name,
            Detail = $"Slug: {o.Slug} • Created: {o.CreatedAt:yyyy-MM-dd}",
            Status = o.IsActive ? "Active" : "Inactive"
        }).ToList();

        return new DashboardContentDto
        {
            Tenant = "Platform",
            Plan = "Super Administrator",
            Section = "Overview",
            Title = "Platform Administration",
            Description = "Multi-tenant system monitoring, center management, and global platform operations.",
            Action = "Organizations",
            Metrics =
            [
                new DashboardMetricDto { Label = "Total Centers", Value = totalOrgs.ToString("N0"), Note = "Registered training centers" },
                new DashboardMetricDto { Label = "Platform Users", Value = totalUsers.ToString("N0"), Note = "System-wide accounts" },
                new DashboardMetricDto { Label = "Active Courses", Value = totalCourses.ToString("N0"), Note = "Across all organizations" },
                new DashboardMetricDto { Label = "Total Learners", Value = totalStudents.ToString("N0"), Note = "Enrolled trainees" }
            ],
            TableTitle = "Registered Training Centers",
            Subtitle = "Active tenant organizations",
            Rows = rows,
            Scope = "Platform-wide",
            Capabilities = ["View all organizations", "System-wide user directory", "Tenant isolation administration"]
        };
    }

    private async Task<DashboardContentDto> BuildOrgAdminAsync(CancellationToken ct)
    {
        var orgId = GetRequiredOrganizationId();

        var org = await _dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orgId, ct);

        var orgName = org?.Name ?? "Organization";

        var totalStudents = await _dbContext.Students
            .AsNoTracking()
            .Where(s => s.OrganizationId == orgId)
            .CountAsync(ct);

        var activeCourses = await _dbContext.Courses
            .AsNoTracking()
            .Where(c => c.OrganizationId == orgId && c.Status == CourseConstants.Statuses.Active)
            .CountAsync(ct);

        var activeCohorts = await _dbContext.CourseGroups
            .AsNoTracking()
            .Where(g => g.OrganizationId == orgId && g.Status == CourseGroupConstants.Statuses.Active)
            .CountAsync(ct);

        var attendanceStats = await GetAttendanceStatisticsAsync(null, ct);

        var cohorts = await _dbContext.CourseGroups
            .AsNoTracking()
            .Where(g => g.OrganizationId == orgId)
            .Include(g => g.Course)
            .OrderByDescending(g => g.CreatedAt)
            .Take(5)
            .ToListAsync(ct);

        var rows = cohorts.Select(g => new DashboardRowDto
        {
            Title = g.Name,
            Detail = $"{g.Course.Name} • {g.ScheduleDescription ?? "Scheduled"}",
            Status = g.Status == CourseGroupConstants.Statuses.Active ? "Active" : "Inactive"
        }).ToList();

        return new DashboardContentDto
        {
            Tenant = orgName,
            Plan = "Training Center",
            Section = "Overview",
            Title = $"{orgName} Overview",
            Description = "Manage courses, cohorts, instructors, and learners for your training center.",
            Action = "Courses",
            Metrics =
            [
                new DashboardMetricDto { Label = "Total Students", Value = totalStudents.ToString("N0"), Note = "Active and registered learners" },
                new DashboardMetricDto { Label = "Active Courses", Value = activeCourses.ToString("N0"), Note = "Curricula offered" },
                new DashboardMetricDto { Label = "Active Cohorts", Value = activeCohorts.ToString("N0"), Note = "Ongoing training batches" },
                new DashboardMetricDto { Label = "Attendance Rate", Value = $"{attendanceStats.AttendancePercentage:F1}%", Note = "Overall center attendance" }
            ],
            TableTitle = "Active Cohorts",
            Subtitle = "Current training batches",
            Rows = rows,
            Scope = "Organization-scoped",
            Capabilities = ["Manage courses and cohorts", "Manage instructors and learners", "Track attendance and sessions"]
        };
    }

    private async Task<DashboardContentDto> BuildInstructorAsync(CancellationToken ct)
    {
        var userId = GetCallerUserId() ?? throw new ForbiddenAccessException("User identity could not be verified.");

        var instructor = await _dbContext.Instructors
            .AsNoTracking()
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(i => i.UserId == userId, ct);

        if (instructor == null)
        {
            throw new NotFoundException("Instructor profile not found.");
        }

        var assignedCohortIds = await _dbContext.CourseGroups
            .AsNoTracking()
            .Where(g => g.InstructorId == instructor.Id)
            .Select(g => g.Id)
            .ToListAsync(ct);

        var assignedCohortsCount = assignedCohortIds.Count;

        var assignedCoursesCount = await _dbContext.CourseGroups
            .AsNoTracking()
            .Where(g => g.InstructorId == instructor.Id)
            .Select(g => g.CourseId)
            .Distinct()
            .CountAsync(ct);

        var instructorStudentsCount = await _dbContext.Enrollments
            .AsNoTracking()
            .Where(e => assignedCohortIds.Contains(e.CourseGroupId))
            .Select(e => e.StudentId)
            .Distinct()
            .CountAsync(ct);

        var upcomingSessions = await _dbContext.Sessions
            .AsNoTracking()
            .Where(s => assignedCohortIds.Contains(s.CourseGroupId) || s.InstructorId == instructor.Id)
            .Include(s => s.CourseGroup)
            .OrderBy(s => s.StartTime)
            .Take(5)
            .ToListAsync(ct);

        var rows = upcomingSessions.Select(s => new DashboardRowDto
        {
            Title = s.CourseGroup?.Name ?? "Session",
            Detail = $"{s.StartTime:yyyy-MM-dd HH:mm} • Room: {s.Room ?? "Online"}",
            Status = s.Status == SessionConstants.Statuses.Completed ? "Completed" : "Scheduled"
        }).ToList();

        return new DashboardContentDto
        {
            Tenant = instructor.Organization?.Name ?? "Training Center",
            Plan = "Instructor",
            Section = "Overview",
            Title = "Instructor Workspace",
            Description = "Manage your assigned cohorts, upcoming class sessions, and student attendance.",
            Action = "Sessions",
            Metrics =
            [
                new DashboardMetricDto { Label = "Assigned Cohorts", Value = assignedCohortsCount.ToString("N0"), Note = "Active teaching groups" },
                new DashboardMetricDto { Label = "Assigned Courses", Value = assignedCoursesCount.ToString("N0"), Note = "Unique courses taught" },
                new DashboardMetricDto { Label = "Enrolled Students", Value = instructorStudentsCount.ToString("N0"), Note = "Across your cohorts" },
                new DashboardMetricDto { Label = "Upcoming Sessions", Value = upcomingSessions.Count.ToString("N0"), Note = "Scheduled classes" }
            ],
            TableTitle = "Upcoming Sessions",
            Subtitle = "Classes scheduled for your cohorts",
            Rows = rows,
            Scope = "Instructor-scoped",
            Capabilities = ["View assigned cohorts", "Mark session attendance", "Review enrolled students"]
        };
    }

    private async Task<DashboardContentDto> BuildStudentAsync(CancellationToken ct)
    {
        var userId = GetCallerUserId() ?? throw new ForbiddenAccessException("User identity could not be verified.");

        var student = await _dbContext.Students
            .AsNoTracking()
            .Include(s => s.Organization)
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (student == null)
        {
            throw new NotFoundException("Student profile not found.");
        }

        var enrollments = await _dbContext.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == student.Id)
            .Include(e => e.CourseGroup)
                .ThenInclude(g => g.Course)
            .ToListAsync(ct);

        var totalEnrolled = enrollments.Count;
        var activeEnrolled = enrollments.Count(e => e.Status == EnrollmentConstants.Statuses.Active);
        var completedEnrolled = enrollments.Count(e => e.Status == EnrollmentConstants.Statuses.Completed);

        var attendanceRecords = await _dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.StudentId == student.Id)
            .ToListAsync(ct);

        var attendedCount = attendanceRecords.Count(a => a.IsAttended);
        var totalRecords = attendanceRecords.Count;
        var attendanceRate = totalRecords > 0 ? (double)attendedCount / totalRecords * 100 : 100.0;

        var rows = enrollments.Take(5).Select(e => new DashboardRowDto
        {
            Title = e.CourseGroup?.Course?.Name ?? "Course",
            Detail = $"Cohort: {e.CourseGroup?.Name ?? "Standard"} • Enrolled: {e.EnrollmentDate:yyyy-MM-dd}",
            Status = e.Status == EnrollmentConstants.Statuses.Active ? "Active" : "Completed"
        }).ToList();

        return new DashboardContentDto
        {
            Tenant = student.Organization?.Name ?? "Training Center",
            Plan = "Student",
            Section = "Overview",
            Title = "Learner Portal",
            Description = "Track your enrolled courses, view upcoming training sessions, and check attendance.",
            Action = "My courses",
            Metrics =
            [
                new DashboardMetricDto { Label = "Enrolled Courses", Value = totalEnrolled.ToString("N0"), Note = "All enrolled programs" },
                new DashboardMetricDto { Label = "Active Courses", Value = activeEnrolled.ToString("N0"), Note = "In-progress cohorts" },
                new DashboardMetricDto { Label = "Completed Courses", Value = completedEnrolled.ToString("N0"), Note = "Finished courses" },
                new DashboardMetricDto { Label = "Attendance Rate", Value = $"{attendanceRate:F1}%", Note = "Personal attendance" }
            ],
            TableTitle = "My Enrolled Courses",
            Subtitle = "Active training programs",
            Rows = rows,
            Scope = "Student-scoped",
            Capabilities = ["Access enrolled courses", "View class schedule", "Monitor personal attendance"]
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
