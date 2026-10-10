using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TrainHub.Application.Common.Exceptions;
using TrainHub.Application.Features.Organizations.DTOs;
using TrainHub.Application.Interfaces;
using TrainHub.Application.Services;
using TrainHub.Domain.Models;
using TrainHub.Infrastructure.Data;

namespace TrainHub.UnitTests.Services;

public class OrganizationServiceTests : IDisposable
{
    private readonly TrainHubDbContext _dbContext;
    private readonly Mock<IOrganizationValidator> _validatorMock;
    private readonly Mock<ILogger<OrganizationService>> _loggerMock;
    private readonly Mock<ICurrentOrganizationService> _currentOrgServiceMock;
    private readonly OrganizationService _service;
    private readonly Guid _platformOrgId = Guid.NewGuid();
    private readonly Guid _clientOrgId = Guid.NewGuid();

    public OrganizationServiceTests()
    {
        _currentOrgServiceMock = new Mock<ICurrentOrganizationService>();
        _currentOrgServiceMock.Setup(s => s.OrganizationId).Returns(_platformOrgId);

        var options = new DbContextOptionsBuilder<TrainHubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TrainHubDbContext(options, _currentOrgServiceMock.Object);
        _validatorMock = new Mock<IOrganizationValidator>();
        _loggerMock = new Mock<ILogger<OrganizationService>>();

        _service = new OrganizationService(_dbContext, _validatorMock.Object, _loggerMock.Object);

        // Seed Platform Org and Client Org
        _dbContext.Organizations.AddRange(
            new Organization { Id = _platformOrgId, Name = "Platform", Slug = "platform", IsActive = true },
            new Organization { Id = _clientOrgId, Name = "Client Center", Slug = "client-center", IsActive = true }
        );
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_WhenSuperAdminHasPlatformOrgContext_ReturnsAllPlatformOrganizations()
    {
        // Act
        var result = (await _service.GetAllAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Select(o => o.Slug).Should().Contain(new[] { "platform", "client-center" });
    }

    [Fact]
    public async Task GetByIdAsync_WhenQueryingAnotherOrganization_ReturnsOrganization()
    {
        // Act
        var result = await _service.GetByIdAsync(_clientOrgId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(_clientOrgId);
        result.Name.Should().Be("Client Center");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ThrowsNotFoundException()
    {
        // Act
        var act = () => _service.GetByIdAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_ValidDto_CreatesAndReturnsOrganization()
    {
        // Arrange
        var dto = new CreateOrganizationDto
        {
            Name = "New Training Center",
            Slug = "new-training-center",
            IsActive = true
        };

        // Act
        var result = await _service.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Slug.Should().Be("new-training-center");

        var created = await _dbContext.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == result.Id);
        created.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_WhenUpdatingAnotherOrganization_UpdatesSuccessfully()
    {
        // Arrange
        var dto = new UpdateOrganizationDto
        {
            Name = "Updated Client Center",
            Slug = "updated-client-center",
            IsActive = true
        };

        // Act
        var result = await _service.UpdateAsync(_clientOrgId, dto);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Client Center");
        result.Slug.Should().Be("updated-client-center");
    }

    [Fact]
    public async Task DeleteAsync_WhenDeletingAnotherOrganization_DeletesSuccessfully()
    {
        // Act
        await _service.DeleteAsync(_clientOrgId);

        // Assert
        var deleted = await _dbContext.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == _clientOrgId);
        deleted.Should().BeNull();
    }
}
