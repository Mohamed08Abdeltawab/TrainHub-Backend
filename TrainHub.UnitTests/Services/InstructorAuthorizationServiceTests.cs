using System.Security.Claims;
using FluentAssertions;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Domain.Constants;
using TrainHub.Infrastructure.Services;

namespace TrainHub.UnitTests.Services;

public class InstructorAuthorizationServiceTests
{
    private readonly InstructorAuthorizationService _service;

    public InstructorAuthorizationServiceTests()
    {
        _service = new InstructorAuthorizationService();
    }

    private static ClaimsPrincipal CreatePrincipal(string role, Guid? userId = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    #region CanManageInstructors

    [Theory]
    [InlineData(AppRoles.SuperAdmin, true)]
    [InlineData(AppRoles.OrgAdmin, true)]
    [InlineData(AppRoles.Instructor, false)]
    [InlineData(AppRoles.Student, false)]
    public void CanManageInstructors_ReturnsExpected(string role, bool expected)
    {
        var principal = CreatePrincipal(role);
        var result = _service.CanManageInstructors(principal);
        result.Should().Be(expected);
    }

    [Fact]
    public void CanManageInstructors_WhenNullUser_ReturnsFalse()
    {
        var result = _service.CanManageInstructors(null!);
        result.Should().BeFalse();
    }

    #endregion

    #region CanViewInstructors

    [Theory]
    [InlineData(AppRoles.SuperAdmin, true)]
    [InlineData(AppRoles.OrgAdmin, true)]
    [InlineData(AppRoles.Instructor, true)]
    [InlineData(AppRoles.Student, false)]
    public void CanViewInstructors_ReturnsExpected(string role, bool expected)
    {
        var principal = CreatePrincipal(role);
        var result = _service.CanViewInstructors(principal);
        result.Should().Be(expected);
    }

    [Fact]
    public void CanViewInstructors_WhenNullUser_ReturnsFalse()
    {
        var result = _service.CanViewInstructors(null!);
        result.Should().BeFalse();
    }

    #endregion

    #region CanModifyInstructor

    [Fact]
    public void CanModifyInstructor_WhenSuperAdmin_ReturnsTrueEvenIfDifferentUser()
    {
        var targetUserId = Guid.NewGuid();
        var principal = CreatePrincipal(AppRoles.SuperAdmin, Guid.NewGuid());

        var result = _service.CanModifyInstructor(principal, targetUserId);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanModifyInstructor_WhenOrgAdmin_ReturnsTrueEvenIfDifferentUser()
    {
        var targetUserId = Guid.NewGuid();
        var principal = CreatePrincipal(AppRoles.OrgAdmin, Guid.NewGuid());

        var result = _service.CanModifyInstructor(principal, targetUserId);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanModifyInstructor_WhenInstructorModifyingSelf_ReturnsTrue()
    {
        var instructorUserId = Guid.NewGuid();
        var principal = CreatePrincipal(AppRoles.Instructor, instructorUserId);

        var result = _service.CanModifyInstructor(principal, instructorUserId);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanModifyInstructor_WhenInstructorModifyingSelfUsingSubClaim_ReturnsTrue()
    {
        var instructorUserId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, AppRoles.Instructor),
            new("sub", instructorUserId.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var result = _service.CanModifyInstructor(principal, instructorUserId);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanModifyInstructor_WhenInstructorModifyingAnotherInstructor_ReturnsFalse()
    {
        var callerUserId = Guid.NewGuid();
        var targetInstructorUserId = Guid.NewGuid();
        var principal = CreatePrincipal(AppRoles.Instructor, callerUserId);

        var result = _service.CanModifyInstructor(principal, targetInstructorUserId);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanModifyInstructor_WhenStudent_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var principal = CreatePrincipal(AppRoles.Student, userId);

        var result = _service.CanModifyInstructor(principal, userId);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanModifyInstructor_WhenNullUser_ReturnsFalse()
    {
        var result = _service.CanModifyInstructor(null!, Guid.NewGuid());
        result.Should().BeFalse();
    }

    #endregion

    #region EnsureCanModifyInstructor

    [Fact]
    public void EnsureCanModifyInstructor_WhenAuthorized_DoesNotThrow()
    {
        var userId = Guid.NewGuid();
        var principal = CreatePrincipal(AppRoles.Instructor, userId);

        var act = () => _service.EnsureCanModifyInstructor(principal, userId);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureCanModifyInstructor_WhenForbidden_ThrowsForbiddenAccessException()
    {
        var callerUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var principal = CreatePrincipal(AppRoles.Instructor, callerUserId);

        var act = () => _service.EnsureCanModifyInstructor(principal, targetUserId);

        act.Should().Throw<ForbiddenAccessException>()
            .WithMessage("You do not have permission to modify another instructor's profile.");
    }

    #endregion
}
