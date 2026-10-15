using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Auth.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;
using TrainHub.Domain.Models;
using TrainHub.IntegrationTests.Infrastructure;
using Xunit;

namespace TrainHub.IntegrationTests.Identity;

[Collection("SqlServer")]
public class SqlServerIdentityUniquenessTests : IClassFixture<SqlServerTestFixture>
{
    private readonly SqlServerTestFixture _fixture;

    public SqlServerIdentityUniquenessTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SameEmail_InDifferentOrganizations_SuccessfullyRegistersAndAuthenticates()
    {
        // ── Arrange: Create two distinct active organizations ──
        var sharedEmail = $"shared_user_{Guid.NewGuid():N}@example.com";
        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();
        var org1Slug = $"org1-{Guid.NewGuid():N}";
        var org2Slug = $"org2-{Guid.NewGuid():N}";

        using (var setupContext = _fixture.CreateNewDbContext())
        {
            var org1 = new Organization
            {
                Id = org1Id,
                Name = $"Tenant One {org1Slug}",
                Slug = org1Slug,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var org2 = new Organization
            {
                Id = org2Id,
                Name = $"Tenant Two {org2Slug}",
                Slug = org2Slug,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            setupContext.Organizations.AddRange(org1, org2);
            await setupContext.SaveChangesAsync();
        }

        // ── Act 1: Register sharedEmail in Organization 1 ──
        using (var scope1 = _fixture.CreateScope(org1Id))
        {
            var authService1 = scope1.ServiceProvider.GetRequiredService<IAuthService>();
            var registerRequest1 = new RegisterRequest
            {
                OrganizationId = org1Id,
                FirstName = "Tenant1",
                LastName = "User",
                Email = sharedEmail,
                Password = "Password123!",
                Role = AppRoles.Student
            };

            var response1 = await authService1.RegisterAsync(registerRequest1);
            response1.Should().NotBeNull();
            response1.Email.Should().Be(sharedEmail);
            response1.OrganizationId.Should().Be(org1Id);
        }

        // ── Act 2: Register same sharedEmail in Organization 2 ──
        // This MUST SUCCEED according to AGENTS.md requirements, demonstrating tenant-scoped email uniqueness on SQL Server
        using (var scope2 = _fixture.CreateScope(org2Id))
        {
            var authService2 = scope2.ServiceProvider.GetRequiredService<IAuthService>();
            var registerRequest2 = new RegisterRequest
            {
                OrganizationId = org2Id,
                FirstName = "Tenant2",
                LastName = "User",
                Email = sharedEmail,
                Password = "Password123!",
                Role = AppRoles.Student
            };

            var response2 = await authService2.RegisterAsync(registerRequest2);
            response2.Should().NotBeNull();
            response2.Email.Should().Be(sharedEmail);
            response2.OrganizationId.Should().Be(org2Id);
        }

        // ── Act 3: Attempt to register same sharedEmail AGAIN in Organization 1 (MUST FAIL) ──
        using (var scope1Duplicate = _fixture.CreateScope(org1Id))
        {
            var authService = scope1Duplicate.ServiceProvider.GetRequiredService<IAuthService>();
            var duplicateRequest = new RegisterRequest
            {
                OrganizationId = org1Id,
                FirstName = "Duplicate",
                LastName = "Attempt",
                Email = sharedEmail,
                Password = "Password123!",
                Role = AppRoles.Student
            };

            var act = () => authService.RegisterAsync(duplicateRequest);
            await act.Should().ThrowAsync<ConflictException>()
                .WithMessage($"*already exists in this organization*");
        }

        // ── Act 4: Login disambiguation testing ──
        using (var loginScope = _fixture.CreateScope())
        {
            var authService = loginScope.ServiceProvider.GetRequiredService<IAuthService>();

            // Attempt login WITHOUT OrganizationId when identical email exists in 2 organizations
            var ambiguousLogin = new LoginRequest
            {
                Email = sharedEmail,
                Password = "Password123!",
                OrganizationId = null
            };

            var ambiguousAct = () => authService.LoginAsync(ambiguousLogin);
            await ambiguousAct.Should().ThrowAsync<BadRequestException>()
                .WithMessage("*Multiple accounts found for this email. Please specify Organization ID.*");

            // Disambiguated login specifying Organization 1
            var org1Login = new LoginRequest
            {
                Email = sharedEmail,
                Password = "Password123!",
                OrganizationId = org1Id
            };
            var org1Auth = await authService.LoginAsync(org1Login);
            org1Auth.OrganizationId.Should().Be(org1Id);

            // Disambiguated login specifying Organization 2
            var org2Login = new LoginRequest
            {
                Email = sharedEmail,
                Password = "Password123!",
                OrganizationId = org2Id
            };
            var org2Auth = await authService.LoginAsync(org2Login);
            org2Auth.OrganizationId.Should().Be(org2Id);
        }

        // ── Verify database state in SQL Server ──
        using (var verifyContext = _fixture.CreateNewDbContext())
        {
            var usersInDb = await verifyContext.ApplicationUsers
                .IgnoreQueryFilters()
                .Where(u => u.Email == sharedEmail)
                .ToListAsync();

            usersInDb.Count.Should().Be(2, "Two distinct user accounts with the same email exist in different organizations.");
            usersInDb.Select(u => u.OrganizationId).Distinct().Count().Should().Be(2);
        }
    }

    [Fact]
    public async Task DatabaseConstraint_DirectDuplicateInsertInSameOrg_ThrowsDbUpdateException()
    {
        var orgId = Guid.NewGuid();
        var email = $"constraint_test_{Guid.NewGuid():N}@test.com";

        using (var context = _fixture.CreateNewDbContext(orgId))
        {
            var org = new Organization
            {
                Id = orgId,
                Name = $"Constraint Org {Guid.NewGuid():N}",
                Slug = $"constraint-org-{Guid.NewGuid():N}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Organizations.Add(org);

            var user1 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                FirstName = "User1",
                LastName = "Test",
                Email = email,
                UserName = email,
                NormalizedEmail = email.ToUpperInvariant(),
                NormalizedUserName = email.ToUpperInvariant(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.ApplicationUsers.Add(user1);
            await context.SaveChangesAsync();

            // Attempt direct insert of duplicate (OrganizationId, NormalizedEmail)
            var user2 = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                FirstName = "User2",
                LastName = "Test",
                Email = email,
                UserName = email,
                NormalizedEmail = email.ToUpperInvariant(),
                NormalizedUserName = email.ToUpperInvariant(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.ApplicationUsers.Add(user2);

            var act = () => context.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>("SQL Server unique index IX_ApplicationUsers_Org_NormalizedEmail must reject duplicates within the same organization.");
        }
    }
}
