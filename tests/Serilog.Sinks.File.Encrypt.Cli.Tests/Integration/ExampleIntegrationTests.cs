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

    [Fact]
    public async Task Generate_QuietAndVerbose_FailsValidationAsUsageError()
    {
        // Act
        CommandAppResult result = await Tester.RunAsync(
            ["generate", "-o", "keys", "-q", "-v"],
            TestContext.Current.CancellationToken
        );

        // Assert - Spectre reports validation failures as -1; Program normalizes to 2
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(ExitCodes.UsageError, ExitCodes.Normalize(result.ExitCode));
        Assert.Contains("--quiet and --verbose cannot be combined", result.Output);
    }

    [Fact]
    public async Task Generate_WithQuiet_ParsesAndSucceeds()
    {
        // Act
        CommandAppResult result = await Tester.RunAsync(
            ["generate", "-o", "keys", "-q"],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(ExitCodes.Success, result.ExitCode);
    }
}
