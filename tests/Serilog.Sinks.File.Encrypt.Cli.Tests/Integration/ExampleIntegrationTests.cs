using Spectre.Console.Cli.Testing;

namespace Serilog.Sinks.File.Encrypt.Cli.Tests.Integration;

/// <summary>
/// Example integration tests for CLI commands
/// </summary>
public class ExampleIntegrationTests : CliIntegrationTestBase
{
    [Fact]
    public async Task Generate_Command_Should_Execute_Successfully()
    {
        // Act
        CommandAppResult result = await Tester.RunAsync(
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
        // Act
        CommandAppResult result = await Tester.RunAsync(
            ["decrypt", "--help"],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("decrypt", result.Output);
    }
}
