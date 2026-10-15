using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Api.Controllers;
using TrainHub.Application.Features.Organizations.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Domain.Constants;

namespace TrainHub.UnitTests.Controllers;

public class OrganizationsControllerTests
{
    private readonly Mock<IOrganizationService> _organizationServiceMock;
    private readonly Mock<ICurrentOrganizationService> _currentOrgServiceMock;
    private readonly Mock<ILogger<OrganizationsController>> _loggerMock;
    private readonly OrganizationsController _controller;
    private readonly Guid _callerOrgId = Guid.NewGuid();
    private readonly Guid _targetOrgId = Guid.NewGuid();

    public OrganizationsControllerTests()
    {
        _organizationServiceMock = new Mock<IOrganizationService>();
        _currentOrgServiceMock = new Mock<ICurrentOrganizationService>();
        _loggerMock = new Mock<ILogger<OrganizationsController>>();

        _controller = new OrganizationsController(
            _organizationServiceMock.Object,
            _currentOrgServiceMock.Object,
            _loggerMock.Object);
    }

    private void SetUserContext(string role, Guid? orgId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role)
        };

        if (orgId.HasValue)
        {
            claims.Add(new Claim("organizationId", orgId.Value.ToString()));
            _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns(orgId.Value);
        }
        else
        {
            _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns((Guid?)null);
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    #region GetCurrent / UpdateCurrent

    [Fact]
    public async Task GetCurrent_WhenNoOrganizationContext_ReturnsBadRequest()
    {
        // Arrange
        SetUserContext(AppRoles.OrgAdmin, null);

        // Act
        var result = await _controller.GetCurrent(CancellationToken.None);

        // Assert
        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var problem = badRequestResult.Value as ProblemDetails;
        problem!.Title.Should().Be("Missing Organization Context");
    }

    [Fact]
    public async Task GetCurrent_WhenValidContext_ReturnsOkWithOrgDto()
    {
        // Arrange
        SetUserContext(AppRoles.OrgAdmin, _callerOrgId);
        var expectedOrg = new OrganizationDto
        {
            Id = _callerOrgId,
            Name = "Current Org",
            Slug = "current-org",
            IsActive = true
        };
        _organizationServiceMock.Setup(s => s.GetByIdAsync(_callerOrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrg);

        // Act
        var result = await _controller.GetCurrent(CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(expectedOrg);
    }

    [Fact]
    public async Task UpdateCurrent_WhenNoOrganizationContext_ReturnsBadRequest()
    {
        // Arrange
        SetUserContext(AppRoles.OrgAdmin, null);
        var dto = new UpdateOrganizationProfileDto { Name = "Updated Name" };

        // Act
        var result = await _controller.UpdateCurrent(dto, CancellationToken.None);

        // Assert
        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UpdateCurrent_WhenValidContext_ReturnsOkWithUpdatedProfile()
    {
        // Arrange
        SetUserContext(AppRoles.OrgAdmin, _callerOrgId);
        var dto = new UpdateOrganizationProfileDto { Name = "Updated Name", Email = "contact@org.com" };
        var updatedOrg = new OrganizationDto
        {
            Id = _callerOrgId,
            Name = "Updated Name",
            Slug = "current-org",
            Email = "contact@org.com",
            IsActive = true
        };
        _organizationServiceMock.Setup(s => s.UpdateProfileAsync(_callerOrgId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedOrg);

        // Act
        var result = await _controller.UpdateCurrent(dto, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(updatedOrg);
    }

    #endregion

    #region GetById / Cross-Tenant Checks

    [Fact]
    public async Task GetById_WhenOrgAdminAccessesOwnOrg_ReturnsOk()
    {
        // Arrange
        SetUserContext(AppRoles.OrgAdmin, _callerOrgId);
        var org = new OrganizationDto { Id = _callerOrgId, Name = "Own Org", Slug = "own-org" };
        _organizationServiceMock.Setup(s => s.GetByIdAsync(_callerOrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(org);

        // Act
        var result = await _controller.GetById(_callerOrgId, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task GetById_WhenOrgAdminAccessesForeignOrg_ReturnsForbid()
    {
        // Arrange
        SetUserContext(AppRoles.OrgAdmin, _callerOrgId);

        // Act
        var result = await _controller.GetById(_targetOrgId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
        _organizationServiceMock.Verify(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetById_WhenSuperAdminAccessesAnyOrg_ReturnsOk()
    {
        // Arrange
        SetUserContext(AppRoles.SuperAdmin, _callerOrgId);
        var foreignOrg = new OrganizationDto { Id = _targetOrgId, Name = "Foreign Org", Slug = "foreign-org" };
        _organizationServiceMock.Setup(s => s.GetByIdAsync(_targetOrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreignOrg);

        // Act
        var result = await _controller.GetById(_targetOrgId, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    #endregion

    #region Update / Privilege Escalation Prevention

    [Fact]
    public async Task Update_WhenOrgAdminAttemptsToModifySlug_ReturnsBadRequest()
    {
        // Arrange
        SetUserContext(AppRoles.OrgAdmin, _callerOrgId);
        var existingOrg = new OrganizationDto
        {
            Id = _callerOrgId,
            Name = "Original Org",
            Slug = "original-slug",
            IsActive = true
        };
        _organizationServiceMock.Setup(s => s.GetByIdAsync(_callerOrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrg);

        var dto = new UpdateOrganizationDto
        {
            Name = "Renamed Org",
            Slug = "tampered-slug", // Changing slug
            IsActive = true
        };

        // Act
        var result = await _controller.Update(_callerOrgId, dto, CancellationToken.None);

        // Assert
        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var problem = badRequestResult.Value as ProblemDetails;
        problem!.Title.Should().Be("Privileged Setting Modification Forbidden");
        _organizationServiceMock.Verify(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateOrganizationDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenOrgAdminAttemptsToModifyIsActive_ReturnsBadRequest()
    {
        // Arrange
        SetUserContext(AppRoles.OrgAdmin, _callerOrgId);
        var existingOrg = new OrganizationDto
        {
            Id = _callerOrgId,
            Name = "Original Org",
            Slug = "original-slug",
            IsActive = true
        };
        _organizationServiceMock.Setup(s => s.GetByIdAsync(_callerOrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrg);

        var dto = new UpdateOrganizationDto
        {
            Name = "Original Org",
            Slug = "original-slug",
            IsActive = false // Changing isActive
        };

        // Act
        var result = await _controller.Update(_callerOrgId, dto, CancellationToken.None);

        // Assert
        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var problem = badRequestResult.Value as ProblemDetails;
        problem!.Title.Should().Be("Privileged Setting Modification Forbidden");
        _organizationServiceMock.Verify(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateOrganizationDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenOrgAdminUpdatesAllowedFieldsOnOwnOrg_ReturnsOk()
    {
        // Arrange
        SetUserContext(AppRoles.OrgAdmin, _callerOrgId);
        var existingOrg = new OrganizationDto
        {
            Id = _callerOrgId,
            Name = "Original Org",
            Slug = "original-slug",
            IsActive = true
        };
        _organizationServiceMock.Setup(s => s.GetByIdAsync(_callerOrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrg);

        var dto = new UpdateOrganizationDto
        {
            Name = "Updated Center Name",
            Slug = "original-slug", // Unchanged
            IsActive = true,        // Unchanged
            Phone = "+123456789"
        };
        var updatedOrg = new OrganizationDto
        {
            Id = _callerOrgId,
            Name = "Updated Center Name",
            Slug = "original-slug",
            IsActive = true,
            Phone = "+123456789"
        };
        _organizationServiceMock.Setup(s => s.UpdateAsync(_callerOrgId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedOrg);

        // Act
        var result = await _controller.Update(_callerOrgId, dto, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(updatedOrg);
    }

    [Fact]
    public async Task Update_WhenSuperAdminModifiesSlugAndIsActive_ReturnsOk()
    {
        // Arrange
        SetUserContext(AppRoles.SuperAdmin, _callerOrgId);
        var dto = new UpdateOrganizationDto
        {
            Name = "Platform Rebranded",
            Slug = "platform-rebranded",
            IsActive = false
        };
        var updatedOrg = new OrganizationDto
        {
            Id = _targetOrgId,
            Name = "Platform Rebranded",
            Slug = "platform-rebranded",
            IsActive = false
        };
        _organizationServiceMock.Setup(s => s.UpdateAsync(_targetOrgId, dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedOrg);

        // Act
        var result = await _controller.Update(_targetOrgId, dto, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(updatedOrg);
    }

    #endregion
}
