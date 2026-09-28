using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Students.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;

namespace TrainHub.Infrastructure.Services;

/// <summary>
/// Service implementation for managing Student entities and operations.
/// </summary>
public class StudentService : IStudentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStudentValidator _studentValidator;
    private readonly ICurrentOrganizationService _currentOrganizationService;
    private readonly TrainHubDbContext _dbContext;
    private readonly ILogger<StudentService> _logger;

    public StudentService(
        IUnitOfWork unitOfWork,
        IStudentValidator studentValidator,
        ICurrentOrganizationService currentOrganizationService,
        TrainHubDbContext dbContext,
        ILogger<StudentService> logger)
    {
        _unitOfWork = unitOfWork;
        _studentValidator = studentValidator;
        _currentOrganizationService = currentOrganizationService;
        _dbContext = dbContext;
        _logger = logger;
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

        var organizationId = dto.OrganizationId ?? _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot create student: Organization ID could not be determined.");
            throw new BadRequestException("Organization ID must be provided or available in the current context.");
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

        await _unitOfWork.Repository<Student>().AddAsync(student, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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

        student.UpdateStudentCode(dto.StudentCode);
        student.UpdateProfile(dto.GuardianPhone, dto.AcademicYear, dto.DateOfBirth, dto.Notes);

        _unitOfWork.Repository<Student>().Update(student);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Student '{StudentId}' updated successfully.", id);

        return MapToDto(student);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting student '{StudentId}'.", id);

        // Enforce dependency and existence rules
        await _studentValidator.ValidateDeleteAsync(id, cancellationToken);

        var student = await _unitOfWork.Repository<Student>().GetByIdAsync(id, cancellationToken);
        if (student == null)
        {
            _logger.LogWarning("Student '{StudentId}' was not found for deletion.", id);
            throw new NotFoundException("Student", id);
        }

        _unitOfWork.Repository<Student>().Delete(student);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
