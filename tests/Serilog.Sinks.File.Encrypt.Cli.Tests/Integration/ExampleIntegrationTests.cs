using Serilog.Sinks.File.Encrypt.Cli.Infrastructure;
using Spectre.Console.Cli.Testing;

namespace Serilog.Sinks.File.Encrypt.Cli.Tests.Integration;

/// <summary>
/// Example integration tests for CLI commands
/// </summary>
public class ExampleIntegrationTests
{
    [Fact]
    public async Task Generate_Command_Should_Execute_Successfully()
    {
        // Arrange - Create a CommandAppTester with the same configuration as the actual app
        TypeRegistrar registrar = CommandAppConfiguration.CreateRegistrar();
        CommandAppTester tester = new(registrar);
        tester.Configure(CommandAppConfiguration.GetConfiguration());

        // Act
        CommandAppResult result = await tester.RunAsync(
            ["generate", "--help"],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("generate", result.Output);
    }

    [Fact]
    public async Task Decrypt_Command_Should_Execute_Successfully()
    {
        // Arrange - Create a CommandAppTester with the same configuration as the actual app
        TypeRegistrar registrar = CommandAppConfiguration.CreateRegistrar();
        CommandAppTester tester = new(registrar);
        tester.Configure(CommandAppConfiguration.GetConfiguration());

        // Act
        CommandAppResult result = await tester.RunAsync(
            ["decrypt", "--help"],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("decrypt", result.Output);
    }
}
