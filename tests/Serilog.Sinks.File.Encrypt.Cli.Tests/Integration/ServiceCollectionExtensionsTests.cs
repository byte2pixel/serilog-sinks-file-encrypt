using Microsoft.Extensions.DependencyInjection;
using Serilog.Sinks.File.Encrypt.Cli.Infrastructure;
using Spectre.Console;

namespace Serilog.Sinks.File.Encrypt.Cli.Tests.Integration;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCliServices_WithoutFileSystem_ShouldRegisterRealFileSystem()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddCliServices();
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        IFileSystem fileSystem = provider.GetRequiredService<IFileSystem>();
        fileSystem.ShouldBeAssignableTo<FileSystem>();
        fileSystem.ShouldNotBeAssignableTo<MockFileSystem>();
    }

    [Fact]
    public void AddCliServices_WithMockFileSystem_ShouldRegisterMockFileSystem()
    {
        // Arrange
        ServiceCollection services = new();
        MockFileSystem mockFileSystem = new();

        // Act
        services.AddCliServices(mockFileSystem);
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        IFileSystem fileSystem = provider.GetRequiredService<IFileSystem>();
        fileSystem.ShouldBeAssignableTo<MockFileSystem>();
        fileSystem.ShouldBe(mockFileSystem);
    }

    [Fact]
    public void AddCliServices_ShouldRegisterExactlyTwoServices()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddCliServices();

        // Assert - Verify no services are accidentally added or removed
        services.Count.ShouldBe(3);

        // Verify IFileSystem is registered
        services
            .FirstOrDefault(s => s.ServiceType == typeof(IFileSystem))
            .ShouldNotBeNull()
            .And(x => x.Lifetime.ShouldBe(ServiceLifetime.Singleton));

        // Verify IAnsiConsole is registered
        services
            .FirstOrDefault(s => s.ServiceType == typeof(IAnsiConsole))
            .ShouldNotBeNull()
            .And(x => x.Lifetime.ShouldBe(ServiceLifetime.Singleton));

        services
            .FirstOrDefault(s => s.ServiceType == typeof(IFileResolver))
            .ShouldNotBeNull()
            .And(x => x.Lifetime.ShouldBe(ServiceLifetime.Transient));
    }

    [Fact]
    public void AddCliServices_CalledMultipleTimes_ShouldNotAddDuplicateServices()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddCliServices();
        services.AddCliServices();

        // Assert
        services.Count.ShouldBe(3); // Should still only have 3 services, no duplicates
    }

    [Fact]
    public void AddCliServices_Returns_ShouldReturnSameServiceCollection()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        IServiceCollection result = services.AddCliServices();

        // Assert
        result.ShouldBe(services); // Should return the same instance for chaining
    }
}
