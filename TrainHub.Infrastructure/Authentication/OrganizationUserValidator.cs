using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrainHub.Domain.Models;

namespace TrainHub.Infrastructure.Authentication;

/// <summary>
/// Validates ApplicationUser instances with tenant-scoped uniqueness rules.
/// In TrainHub SaaS, username and email uniqueness are scoped to the user's OrganizationId,
/// allowing the same email/username to exist across distinct organizations as required by AGENTS.md.
/// </summary>
public class OrganizationUserValidator : IUserValidator<ApplicationUser>
{
    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);

        var errors = new List<IdentityError>();

        await ValidateUserNameAsync(manager, user, errors);
        await ValidateEmailAsync(manager, user, errors);

        return errors.Count > 0 ? IdentityResult.Failed(errors.ToArray()) : IdentityResult.Success;
    }

    private static async Task ValidateUserNameAsync(UserManager<ApplicationUser> manager, ApplicationUser user, List<IdentityError> errors)
    {
        var userName = await manager.GetUserNameAsync(user);
        if (string.IsNullOrWhiteSpace(userName))
        {
            errors.Add(manager.ErrorDescriber.InvalidUserName(userName));
            return;
        }

        if (!string.IsNullOrEmpty(manager.Options.User.AllowedUserNameCharacters) &&
            userName.Any(c => !manager.Options.User.AllowedUserNameCharacters.Contains(c)))
        {
            errors.Add(manager.ErrorDescriber.InvalidUserName(userName));
            return;
        }

        var normalizedUserName = manager.NormalizeName(userName);
        var existingUser = await manager.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName && u.OrganizationId == user.OrganizationId);

        if (existingUser != null && !string.Equals(await manager.GetUserIdAsync(existingUser), await manager.GetUserIdAsync(user)))
        {
            errors.Add(manager.ErrorDescriber.DuplicateUserName(userName));
        }
    }

    private static async Task ValidateEmailAsync(UserManager<ApplicationUser> manager, ApplicationUser user, List<IdentityError> errors)
    {
        var email = await manager.GetEmailAsync(user);
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var normalizedEmail = manager.NormalizeEmail(email);
        var existingUser = await manager.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail && u.OrganizationId == user.OrganizationId);

        if (existingUser != null && !string.Equals(await manager.GetUserIdAsync(existingUser), await manager.GetUserIdAsync(user)))
        {
            errors.Add(manager.ErrorDescriber.DuplicateEmail(email));
        }
    }
}
