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
        Assert.NotNull(fileSystem);
        Assert.IsType<FileSystem>(fileSystem);
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
        Assert.NotNull(fileSystem);
        Assert.IsType<MockFileSystem>(fileSystem);
        Assert.Same(mockFileSystem, fileSystem);
    }

    [Fact]
    public void AddCliServices_ShouldRegisterAnsiConsole()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddCliServices();
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        IAnsiConsole console = provider.GetRequiredService<IAnsiConsole>();
        Assert.NotNull(console);
        Assert.Same(AnsiConsole.Console, console);
    }

    [Fact]
    public void AddCliServices_ShouldRegisterExactlyTwoServices()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddCliServices();

        // Assert - Verify no services are accidentally added or removed
        Assert.Equal(2, services.Count);

        // Verify IFileSystem is registered
        ServiceDescriptor? fileSystemDescriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IFileSystem)
        );
        Assert.NotNull(fileSystemDescriptor);
        Assert.Equal(ServiceLifetime.Transient, fileSystemDescriptor.Lifetime);

        // Verify IAnsiConsole is registered
        ServiceDescriptor? consoleDescriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IAnsiConsole)
        );
        Assert.NotNull(consoleDescriptor);
        Assert.Equal(ServiceLifetime.Singleton, consoleDescriptor.Lifetime);
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
        Assert.Equal(2, services.Count);
    }

    [Fact]
    public void AddCliServices_WithMockFileSystem_ShouldUseMockInstance()
    {
        // Arrange
        ServiceCollection services = new();
        MockFileSystem mockFileSystem = new();
        services.AddCliServices(mockFileSystem);
        ServiceProvider provider = services.BuildServiceProvider();

        // Act
        IFileSystem fileSystem = provider.GetRequiredService<IFileSystem>();

        // Assert - Should return the exact mock instance provided
        Assert.Same(mockFileSystem, fileSystem);
    }
}
