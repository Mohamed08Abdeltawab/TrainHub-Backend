using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Common.Models;
using TrainHub.Application.Features.Instructors.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;

namespace TrainHub.Infrastructure.Services;

/// <summary>
/// Service implementation for managing Instructor entities and operations.
/// </summary>
public class InstructorService : IInstructorService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInstructorValidator _instructorValidator;
    private readonly ICurrentOrganizationService _currentOrganizationService;
    private readonly TrainHubDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<InstructorService> _logger;

    public InstructorService(
        IUnitOfWork unitOfWork,
        IInstructorValidator instructorValidator,
        ICurrentOrganizationService currentOrganizationService,
        TrainHubDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<InstructorService> logger)
    {
        _unitOfWork = unitOfWork;
        _instructorValidator = instructorValidator;
        _currentOrganizationService = currentOrganizationService;
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<PagedResult<InstructorDto>> GetPagedAsync(InstructorQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        parameters ??= new InstructorQueryParameters();

        _logger.LogInformation("Retrieving paged instructors: Page {PageNumber}, Size {PageSize}, Sort {SortBy} (Desc: {Desc}).",
            parameters.PageNumber, parameters.PageSize, parameters.SortBy, parameters.SortDescending);

        // 1. Validate query parameters
        _instructorValidator.ValidateQuery(parameters);

        // 2. Base query with user details (tenant filter applied automatically)
        var query = _dbContext.Instructors
            .AsNoTracking()
            .Include(i => i.User)
            .AsQueryable();

        // 3. Search filter
        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var term = parameters.SearchTerm.Trim();
            query = query.Where(i =>
                i.Specialization.Contains(term) ||
                (i.Bio != null && i.Bio.Contains(term)) ||
                i.User.FirstName.Contains(term) ||
                i.User.LastName.Contains(term) ||
                (i.User.Email != null && i.User.Email.Contains(term)) ||
                (i.User.PhoneNumber != null && i.User.PhoneNumber.Contains(term)));
        }

        // 4. Specific field filters
        if (!string.IsNullOrWhiteSpace(parameters.Specialization))
        {
            var spec = parameters.Specialization.Trim();
            query = query.Where(i => i.Specialization.Contains(spec));
        }

        if (parameters.IsActive.HasValue)
        {
            query = query.Where(i => i.User.IsActive == parameters.IsActive.Value);
        }

        if (parameters.HasBio.HasValue)
        {
            query = parameters.HasBio.Value
                ? query.Where(i => i.Bio != null && i.Bio != "")
                : query.Where(i => i.Bio == null || i.Bio == "");
        }

        // 5. Total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // 6. Sorting
        var isDescending = parameters.SortDescending;
        query = (parameters.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "specialization" => isDescending ? query.OrderByDescending(i => i.Specialization) : query.OrderBy(i => i.Specialization),
            "firstname" => isDescending ? query.OrderByDescending(i => i.User.FirstName) : query.OrderBy(i => i.User.FirstName),
            "lastname" => isDescending ? query.OrderByDescending(i => i.User.LastName) : query.OrderBy(i => i.User.LastName),
            "email" => isDescending ? query.OrderByDescending(i => i.User.Email) : query.OrderBy(i => i.User.Email),
            "updatedat" => isDescending ? query.OrderByDescending(i => i.UpdatedAt) : query.OrderBy(i => i.UpdatedAt),
            _ => isDescending ? query.OrderByDescending(i => i.CreatedAt) : query.OrderBy(i => i.CreatedAt)
        };

        // 7. Pagination
        var skip = (parameters.PageNumber - 1) * parameters.PageSize;
        var items = await query
            .Skip(skip)
            .Take(parameters.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();

        return new PagedResult<InstructorDto>(dtos, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<InstructorDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving instructor with ID '{InstructorId}'.", id);

        var instructor = await _dbContext.Instructors
            .AsNoTracking()
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (instructor == null)
        {
            _logger.LogWarning("Instructor '{InstructorId}' was not found.", id);
            throw new NotFoundException("Instructor", id);
        }

        return MapToDto(instructor);
    }

    public async Task<InstructorDto> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving instructor linked to user ID '{UserId}'.", userId);

        var instructor = await _dbContext.Instructors
            .AsNoTracking()
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.UserId == userId, cancellationToken);

        if (instructor == null)
        {
            _logger.LogWarning("No instructor was found linked to user ID '{UserId}'.", userId);
            throw new NotFoundException("Instructor", $"UserId: {userId}");
        }

        return MapToDto(instructor);
    }

    public async Task<InstructorDto> GetCurrentInstructorAsync(Guid currentUserId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving current instructor profile for user ID '{UserId}'.", currentUserId);
        return await GetByUserIdAsync(currentUserId, cancellationToken);
    }

    public async Task<InstructorDto> UpdateCurrentInstructorAsync(Guid currentUserId, UpdateInstructorDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        _logger.LogInformation("Updating current instructor profile for user ID '{UserId}'.", currentUserId);

        var instructor = await _dbContext.Instructors
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.UserId == currentUserId, cancellationToken);

        if (instructor == null)
        {
            _logger.LogWarning("No instructor profile found for current user ID '{UserId}' to update.", currentUserId);
            throw new NotFoundException("Instructor", $"UserId: {currentUserId}");
        }

        return await UpdateAsync(instructor.Id, dto, cancellationToken);
    }

    public async Task<IEnumerable<InstructorDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all instructors within current tenant scope.");

        var instructors = await _dbContext.Instructors
            .AsNoTracking()
            .Include(i => i.User)
            .ToListAsync(cancellationToken);

        return instructors.Select(MapToDto).ToList();
    }

    public async Task<InstructorDto> CreateAsync(CreateInstructorDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var organizationId = dto.OrganizationId ?? _currentOrganizationService.OrganizationId;
        if (!organizationId.HasValue || organizationId.Value == Guid.Empty)
        {
            _logger.LogWarning("Cannot create instructor: Organization ID could not be determined.");
            throw new BadRequestException("Organization ID must be provided or available in the current context.");
        }

        _logger.LogInformation("Creating instructor for user '{UserId}' in organization '{OrganizationId}'.",
            dto.UserId, organizationId.Value);

        // Enforce business rules and uniqueness
        await _instructorValidator.ValidateCreateAsync(dto, organizationId.Value, cancellationToken);

        var instructor = Instructor.Create(
            dto.UserId,
            organizationId.Value,
            dto.Specialization,
            dto.Bio);

        await _unitOfWork.Repository<Instructor>().AddAsync(instructor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Instructor '{InstructorId}' created successfully.", instructor.Id);

        // Assign Instructor role if not already assigned
        var identityUser = await _userManager.FindByIdAsync(dto.UserId.ToString());
        if (identityUser != null && !await _userManager.IsInRoleAsync(identityUser, AppRoles.Instructor))
        {
            await _userManager.AddToRoleAsync(identityUser, AppRoles.Instructor);
            _logger.LogInformation("Assigned Instructor role to user '{UserId}'.", dto.UserId);
        }

        var user = await _dbContext.ApplicationUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == dto.UserId, cancellationToken);

        return MapToDto(instructor, user ?? identityUser);
    }

    public async Task<InstructorDto> UpdateAsync(Guid id, UpdateInstructorDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        _logger.LogInformation("Updating instructor '{InstructorId}'.", id);

        var instructor = await _dbContext.Instructors
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (instructor == null)
        {
            _logger.LogWarning("Instructor '{InstructorId}' was not found for update.", id);
            throw new NotFoundException("Instructor", id);
        }

        // Enforce business rules
        await _instructorValidator.ValidateUpdateAsync(id, dto, instructor.OrganizationId, cancellationToken);

        instructor.UpdateProfile(dto.Specialization, dto.Bio);

        _unitOfWork.Repository<Instructor>().Update(instructor);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Instructor '{InstructorId}' updated successfully.", id);

        return MapToDto(instructor);
    }

    public async Task<InstructorDto> LinkUserAsync(Guid id, LinkInstructorUserDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return await LinkUserAsync(id, dto.UserId, cancellationToken);
    }

    public async Task<InstructorDto> LinkUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Linking instructor '{InstructorId}' to user '{UserId}'.", id, userId);

        var instructor = await _dbContext.Instructors
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (instructor == null)
        {
            _logger.LogWarning("Instructor '{InstructorId}' was not found for user linking.", id);
            throw new NotFoundException("Instructor", id);
        }

        // Validate linking rules
        await _instructorValidator.ValidateLinkUserAsync(id, userId, instructor.OrganizationId, cancellationToken);

        var oldUserId = instructor.UserId;

        instructor.LinkUser(userId);

        _unitOfWork.Repository<Instructor>().Update(instructor);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Role management on user re-linking
        if (oldUserId != userId)
        {
            var oldUserStillInstructor = await _dbContext.Instructors
                .IgnoreQueryFilters()
                .AnyAsync(i => i.UserId == oldUserId && i.Id != id, cancellationToken);

            if (!oldUserStillInstructor)
            {
                var oldUser = await _userManager.FindByIdAsync(oldUserId.ToString());
                if (oldUser != null && await _userManager.IsInRoleAsync(oldUser, AppRoles.Instructor))
                {
                    await _userManager.RemoveFromRoleAsync(oldUser, AppRoles.Instructor);
                    _logger.LogInformation("Removed Instructor role from previous user '{UserId}'.", oldUserId);
                }
            }
        }

        var newUser = await _userManager.FindByIdAsync(userId.ToString());
        if (newUser != null && !await _userManager.IsInRoleAsync(newUser, AppRoles.Instructor))
        {
            await _userManager.AddToRoleAsync(newUser, AppRoles.Instructor);
            _logger.LogInformation("Assigned Instructor role to newly linked user '{UserId}'.", userId);
        }

        var user = await _dbContext.ApplicationUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        _logger.LogInformation("Instructor '{InstructorId}' successfully linked to user '{UserId}'.", id, userId);

        return MapToDto(instructor, user ?? newUser);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting instructor '{InstructorId}'.", id);

        // Enforce dependency and existence rules
        await _instructorValidator.ValidateDeleteAsync(id, cancellationToken);

        var instructor = await _unitOfWork.Repository<Instructor>().GetByIdAsync(id, cancellationToken);
        if (instructor == null)
        {
            _logger.LogWarning("Instructor '{InstructorId}' was not found for deletion.", id);
            throw new NotFoundException("Instructor", id);
        }

        var userId = instructor.UserId;

        _unitOfWork.Repository<Instructor>().Delete(instructor);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // If the user no longer has any other instructor profile, remove the Instructor role
        var stillInstructor = await _dbContext.Instructors
            .IgnoreQueryFilters()
            .AnyAsync(i => i.UserId == userId && i.Id != id, cancellationToken);

        if (!stillInstructor)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user != null && await _userManager.IsInRoleAsync(user, AppRoles.Instructor))
            {
                await _userManager.RemoveFromRoleAsync(user, AppRoles.Instructor);
                _logger.LogInformation("Removed Instructor role from user '{UserId}' after instructor deletion.", userId);
            }
        }

        _logger.LogInformation("Instructor '{InstructorId}' deleted successfully.", id);
    }

    private static InstructorDto MapToDto(Instructor instructor) => MapToDto(instructor, null);

    private static InstructorDto MapToDto(Instructor instructor, ApplicationUser? user)
    {
        var resolvedUser = user ?? instructor.User;

        return new InstructorDto
        {
            Id = instructor.Id,
            UserId = instructor.UserId,
            OrganizationId = instructor.OrganizationId,
            Specialization = instructor.Specialization,
            Bio = instructor.Bio,
            CreatedAt = instructor.CreatedAt,
            UpdatedAt = instructor.UpdatedAt,
            FirstName = resolvedUser?.FirstName,
            LastName = resolvedUser?.LastName,
            Email = resolvedUser?.Email,
            PhoneNumber = resolvedUser?.PhoneNumber
        };
    }
}
