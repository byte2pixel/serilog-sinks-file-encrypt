using Spectre.Console.Cli.Testing;

namespace Serilog.Sinks.File.Encrypt.Cli.Tests.Integration;

/// <summary>
/// Guards <see cref="CommandAppConfiguration"/>'s <c>UseStrictParsing()</c>: unknown options
/// must be rejected as a usage error rather than silently ignored. Without strict parsing
/// Spectre.Console.Cli collects unrecognized options into the remaining arguments and the
/// command runs on with its defaults (e.g. <c>generate -k 4096</c> would silently keep the
/// default key size).
/// </summary>
public class StrictParsingTests : CliIntegrationTestBase
{
    [Fact]
    public async Task Generate_UnknownShortOption_IsRejectedAsUsageError()
    {
        // Act — '-k' is not a defined option; only '--key-size' is.
        CommandAppResult result = await Tester.RunAsync(
            ["generate", "-o", "keys", "-k", "4096", "--plaintext"],
            TestContext.Current.CancellationToken
        );

        // Assert — Spectre reports parse failures as -1; Program normalizes to 2.
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(ExitCodes.UsageError, ExitCodes.Normalize(result.ExitCode));
        Assert.Contains("Unknown option", result.Output);
    }

    [Fact]
    public async Task Generate_KeySizeLongOption_IsStillAccepted()
    {
        // Act — the supported spelling must keep working under strict parsing.
        CommandAppResult result = await Tester.RunAsync(
            ["generate", "-o", "keys", "--key-size", "4096", "--plaintext"],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(ExitCodes.Success, result.ExitCode);
    }

    [Fact]
    public async Task Decrypt_UnknownOption_IsRejectedAsUsageError()
    {
        // Act
        CommandAppResult result = await Tester.RunAsync(
            ["decrypt", "app.log", "-k", "private_key.pem", "--nope"],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(ExitCodes.UsageError, ExitCodes.Normalize(result.ExitCode));
        Assert.Contains("Unknown option", result.Output);
    }
}
