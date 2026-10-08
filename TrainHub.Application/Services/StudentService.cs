using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Students.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Models;

namespace TrainHub.Application.Services;

/// <summary>
/// Service implementation for managing Student entities and operations.
/// </summary>
public class StudentService : IStudentService
{
    private readonly ITrainHubDbContext _dbContext;
    private readonly IStudentValidator _studentValidator;
    private readonly ICurrentOrganizationService _currentOrganizationService;
    private readonly ILogger<StudentService> _logger;

    public StudentService(
        ITrainHubDbContext dbContext,
        IStudentValidator studentValidator,
        ICurrentOrganizationService currentOrganizationService,
        ILogger<StudentService> logger)
    {
        _dbContext = dbContext;
        _studentValidator = studentValidator;
        _currentOrganizationService = currentOrganizationService;
        _logger = logger;
    }

    public async Task<PagedResult<StudentDto>> GetPagedAsync(StudentQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        parameters ??= new StudentQueryParameters();

        _logger.LogInformation("Retrieving paged students: Page {PageNumber}, Size {PageSize}, Sort {SortBy} (Desc: {Desc}).",
            parameters.PageNumber, parameters.PageSize, parameters.SortBy, parameters.SortDescending);

        // 1. Validate query parameters
        _studentValidator.ValidateQuery(parameters);

        // 2. Base query with user details (tenant filter applied automatically)
        var query = _dbContext.Students
            .AsNoTracking()
            .Include(s => s.User)
            .AsQueryable();

        // 3. Search filter
        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var term = parameters.SearchTerm.Trim();
            query = query.Where(s =>
                s.StudentCode.Contains(term) ||
                (s.GuardianPhone != null && s.GuardianPhone.Contains(term)) ||
                s.User.FirstName.Contains(term) ||
                s.User.LastName.Contains(term) ||
                (s.User.Email != null && s.User.Email.Contains(term)));
        }

        // 4. Specific field filters
        if (!string.IsNullOrWhiteSpace(parameters.AcademicYear))
        {
            var year = parameters.AcademicYear.Trim();
            query = query.Where(s => s.AcademicYear == year);
        }

        if (parameters.DateOfBirthFrom.HasValue)
        {
            query = query.Where(s => s.DateOfBirth >= parameters.DateOfBirthFrom.Value);
        }

        if (parameters.DateOfBirthTo.HasValue)
        {
            query = query.Where(s => s.DateOfBirth <= parameters.DateOfBirthTo.Value);
        }

        if (parameters.HasGuardianPhone.HasValue)
        {
            query = parameters.HasGuardianPhone.Value
                ? query.Where(s => s.GuardianPhone != null && s.GuardianPhone != "")
                : query.Where(s => s.GuardianPhone == null || s.GuardianPhone == "");
        }

        if (parameters.IsActive.HasValue)
        {
            query = query.Where(s => s.User.IsActive == parameters.IsActive.Value);
        }

        // 5. Total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // 6. Sorting
        var isDescending = parameters.SortDescending;
        query = (parameters.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "studentcode" => isDescending ? query.OrderByDescending(s => s.StudentCode) : query.OrderBy(s => s.StudentCode),
            "firstname" => isDescending ? query.OrderByDescending(s => s.User.FirstName) : query.OrderBy(s => s.User.FirstName),
            "lastname" => isDescending ? query.OrderByDescending(s => s.User.LastName) : query.OrderBy(s => s.User.LastName),
            "email" => isDescending ? query.OrderByDescending(s => s.User.Email) : query.OrderBy(s => s.User.Email),
            "academicyear" => isDescending ? query.OrderByDescending(s => s.AcademicYear) : query.OrderBy(s => s.AcademicYear),
            "dateofbirth" => isDescending ? query.OrderByDescending(s => s.DateOfBirth) : query.OrderBy(s => s.DateOfBirth),
            "updatedat" => isDescending ? query.OrderByDescending(s => s.UpdatedAt) : query.OrderBy(s => s.UpdatedAt),
            _ => isDescending ? query.OrderByDescending(s => s.CreatedAt) : query.OrderBy(s => s.CreatedAt)
        };

        // 7. Pagination
        var skip = (parameters.PageNumber - 1) * parameters.PageSize;
        var items = await query
            .Skip(skip)
            .Take(parameters.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();

        return new PagedResult<StudentDto>(dtos, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<StudentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving student with ID '{StudentId}'.", id);

        var student = await _dbContext.Students
            .AsNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student == null)
        {
            _logger.LogWarning("Student '{StudentId}' was not found.", id);
            throw new NotFoundException("Student", id);
        }

        return MapToDto(student);
    }

    public async Task<IEnumerable<StudentDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all students within current tenant scope.");

        var students = await _dbContext.Students
            .AsNoTracking()
            .Include(s => s.User)
            .ToListAsync(cancellationToken);

        return students.Select(MapToDto).ToList();
    }

    public async Task<StudentDto> CreateAsync(CreateStudentDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var organizationId = _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot create student: Organization ID could not be determined from current context.");
            throw new BadRequestException("Organization context is required.");
        }

        _logger.LogInformation("Creating student for user '{UserId}' in organization '{OrganizationId}' with code '{StudentCode}'.",
            dto.UserId, organizationId.Value, dto.StudentCode);

        // Enforce business rules and uniqueness
        await _studentValidator.ValidateCreateAsync(dto, organizationId.Value, cancellationToken);

        var student = Student.Create(
            dto.UserId,
            organizationId.Value,
            dto.StudentCode,
            dto.GuardianPhone,
            dto.AcademicYear,
            dto.DateOfBirth,
            dto.Notes);

        await _dbContext.Students.AddAsync(student, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Student '{StudentId}' created successfully.", student.Id);

        var user = await _dbContext.ApplicationUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == dto.UserId, cancellationToken);

        return MapToDto(student, user);
    }

    public async Task<StudentDto> UpdateAsync(Guid id, UpdateStudentDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        _logger.LogInformation("Updating student '{StudentId}'.", id);

        var student = await _dbContext.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student == null)
        {
            _logger.LogWarning("Student '{StudentId}' was not found for update.", id);
            throw new NotFoundException("Student", id);
        }

        // Enforce business rules and uniqueness
        await _studentValidator.ValidateUpdateAsync(id, dto, student.OrganizationId, cancellationToken);

        student.UpdateProfile(dto.StudentCode, dto.GuardianPhone, dto.AcademicYear, dto.DateOfBirth, dto.Notes);

        if (student.User != null)
        {
            if (!string.IsNullOrWhiteSpace(dto.FirstName))
                student.User.FirstName = dto.FirstName.Trim();

            if (!string.IsNullOrWhiteSpace(dto.LastName))
                student.User.LastName = dto.LastName.Trim();

            if (dto.PhoneNumber != null)
                student.User.PhoneNumber = dto.PhoneNumber.Trim();
        }

        _dbContext.Students.Update(student);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Student '{StudentId}' updated successfully.", id);

        return MapToDto(student);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting student '{StudentId}'.", id);

        var student = await _dbContext.Students.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (student == null)
        {
            _logger.LogWarning("Student '{StudentId}' was not found for deletion.", id);
            throw new NotFoundException("Student", id);
        }

        // Enforce dependency and existence rules
        await _studentValidator.ValidateDeleteAsync(id, cancellationToken);

        _dbContext.Students.Remove(student);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Student '{StudentId}' deleted successfully.", id);
    }

    private static StudentDto MapToDto(Student student) => MapToDto(student, null);

    private static StudentDto MapToDto(Student student, ApplicationUser? user)
    {
        var resolvedUser = user ?? student.User;

        return new StudentDto
        {
            Id = student.Id,
            UserId = student.UserId,
            OrganizationId = student.OrganizationId,
            StudentCode = student.StudentCode,
            GuardianPhone = student.GuardianPhone,
            AcademicYear = student.AcademicYear,
            DateOfBirth = student.DateOfBirth,
            Age = student.CalculateAge(),
            Notes = student.Notes,
            CreatedAt = student.CreatedAt,
            UpdatedAt = student.UpdatedAt,
            FirstName = resolvedUser?.FirstName,
            LastName = resolvedUser?.LastName,
            Email = resolvedUser?.Email,
            PhoneNumber = resolvedUser?.PhoneNumber
        };
    }
}
