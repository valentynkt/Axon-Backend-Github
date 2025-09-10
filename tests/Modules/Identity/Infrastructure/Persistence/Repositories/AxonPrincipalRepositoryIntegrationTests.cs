using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.DependencyInjection;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.Repositories;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Write;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Axon.Modules.Identity.Infrastructure.Tests.Persistence.Repositories;

/// <summary>
/// Integration tests for AxonPrincipal repositories.
/// Tests repository functionality with actual EF Core context and database constraints.
/// </summary>
[TestFixture]
public class AxonPrincipalRepositoryIntegrationTests
{
    private ServiceProvider _serviceProvider = null!;
    private IdentityWriteDbContext _context = null!;
    private AxonPrincipalWriteRepository _writeRepository = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        
        // Configuration for testing
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test"
            });
        var config = configBuilder.Build();

        // Add EF Core with InMemory database for testing
        services.AddDbContext<IdentityWriteDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()) // Unique DB per test
                   .UseSnakeCaseNamingConvention());
                   

        // Add repositories
        services.AddScoped<AxonPrincipalWriteRepository>();
        
        // Add UnitOfWork  
        services.AddScoped<IWriteUnitOfWork<Axon.Modules.Identity.Application.Common.Models.IdentityModule>, 
            EfUnitOfWork<IdentityWriteDbContext, Axon.Modules.Identity.Application.Common.Models.IdentityModule>>();
        services.AddScoped<IWriteUnitOfWork>(provider => 
            provider.GetRequiredService<IWriteUnitOfWork<Axon.Modules.Identity.Application.Common.Models.IdentityModule>>());

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<IdentityWriteDbContext>();
        _writeRepository = _serviceProvider.GetRequiredService<AxonPrincipalWriteRepository>();

        // Ensure database is created with schema
        _context.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _writeRepository?.Dispose();
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }

    [Test]
    public async Task AddAsync_ShouldCreatePrincipalSuccessfully()
    {
        // Arrange
        var email = "test@example.com";
        var emailHash = EmailHash.FromEmail(email);
        var principalResult = AxonPrincipal.CreateHumanPrincipal(emailHash);
        
        principalResult.IsSuccess.Should().BeTrue();
        var principal = principalResult.Value;

        // Act
        var addedPrincipal = await _writeRepository.AddAsync(principal);
        await _writeRepository.UnitOfWork.SaveChangesAsync();

        // Assert
        addedPrincipal.Should().NotBeNull();
        addedPrincipal.Id.Should().Be(principal.Id);
        addedPrincipal.PrimaryEmailHash.Should().Be(emailHash);
        addedPrincipal.IsHuman.Should().BeTrue();
    }

    [Test]
    public async Task FindByEmailHashAsync_ShouldReturnMatchingPrincipals()
    {
        // Arrange
        var email = "unique@example.com";
        var emailHash = EmailHash.FromEmail(email);
        var principalResult = AxonPrincipal.CreateHumanPrincipal(emailHash);
        
        var principal = principalResult.Value;
        await _writeRepository.AddAsync(principal);
        await _writeRepository.UnitOfWork.SaveChangesAsync();

        // Act
        var foundPrincipals = await _writeRepository.FindByEmailHashAsync(emailHash);

        // Assert
        foundPrincipals.Should().NotBeNull();
        foundPrincipals.Should().HaveCount(1);
        foundPrincipals.First().PrimaryEmailHash.Should().Be(emailHash);
    }

    [Test] 
    public async Task GetByIdAsync_ShouldReturnCorrectPrincipal()
    {
        // Arrange
        var principalResult = AxonPrincipal.CreateServicePrincipal();
        var principal = principalResult.Value;
        
        await _writeRepository.AddAsync(principal);
        await _writeRepository.UnitOfWork.SaveChangesAsync();

        // Act
        var foundPrincipal = await _writeRepository.GetByIdAsync(principal.Id);

        // Assert
        foundPrincipal.Should().NotBeNull();
        foundPrincipal!.Id.Should().Be(principal.Id);
        foundPrincipal.IsService.Should().BeTrue();
    }

    [Test]
    public async Task IsCredentialTakenAsync_ShouldReturnTrueForExistingCredential()
    {
        // Arrange
        var principal = AxonPrincipal.CreateHumanPrincipal().Value;
        var providerType = ProviderType.From("dynamic");
        var issuer = "test-issuer";
        var subject = "test-subject";

        var credentialResult = principal.LinkIdentityCredential(
            providerType, issuer, subject, 
            environmentId: null, timeProvider: TimeProvider.System);
        
        credentialResult.IsSuccess.Should().BeTrue();
        
        await _writeRepository.AddAsync(principal);
        await _writeRepository.UnitOfWork.SaveChangesAsync();

        // Act
        var isTaken = await _writeRepository.IsCredentialTakenAsync(providerType, issuer, subject);

        // Assert
        isTaken.Should().BeTrue();
    }

    [Test]
    public async Task IsCredentialTakenAsync_ShouldReturnFalseForNonExistentCredential()
    {
        // Arrange
        var providerType = ProviderType.From("siws");
        var issuer = "non-existent-issuer";
        var subject = "non-existent-subject";

        // Act
        var isTaken = await _writeRepository.IsCredentialTakenAsync(providerType, issuer, subject);

        // Assert
        isTaken.Should().BeFalse();
    }

    [Test]
    public async Task FindByCredentialAsync_ShouldReturnPrincipalWithMatchingCredential()
    {
        // Arrange
        var principal = AxonPrincipal.CreateHumanPrincipal().Value;
        var providerType = ProviderType.From("oidc");
        var issuer = "credential-issuer";
        var subject = "credential-subject";

        var credentialResult = principal.LinkIdentityCredential(
            providerType, issuer, subject,
            environmentId: null, timeProvider: TimeProvider.System);
        
        credentialResult.IsSuccess.Should().BeTrue();
        
        await _writeRepository.AddAsync(principal);
        await _writeRepository.UnitOfWork.SaveChangesAsync();

        // Act
        var foundPrincipal = await _writeRepository.FindByCredentialAsync(providerType, issuer, subject);

        // Assert
        foundPrincipal.Should().NotBeNull();
        foundPrincipal!.Id.Should().Be(principal.Id);
        foundPrincipal.Credentials.Should().HaveCount(1);
        var credential = foundPrincipal.Credentials.First();
        credential.ProviderType.Should().Be(providerType);
        credential.Issuer.Should().Be(issuer);
        credential.Subject.Should().Be(subject);
    }
}