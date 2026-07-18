using Spectre.Console.Cli.Testing;

namespace Serilog.Sinks.File.Encrypt.Cli.Tests.Integration;

/// <summary>
/// Guards <see cref="CommandAppConfiguration"/>'s <c>SetApplicationVersion(...)</c>. With
/// strict parsing enabled the app-level <c>--version</c> option must be explicitly configured,
/// otherwise it would be rejected as an unknown option. The version flag lives at the root
/// (before any command); <c>-v</c> inside a command still means <c>--verbose</c>.
/// </summary>
public class VersionOptionTests : CliIntegrationTestBase
{
    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public async Task RootVersionOption_PrintsVersion_ExitZero(string flag)
    {
        // Act
        CommandAppResult result = await Tester.RunAsync(
            [flag],
            TestContext.Current.CancellationToken
        );

        // Assert — resolves the assembly version; must not be an "Unknown option" failure.
        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Output));
        Assert.DoesNotContain("Unknown option", result.Output);
    }

    [Fact]
    public async Task CommandLevelVerboseShortFlag_IsNotTreatedAsVersion()
    {
        // Act — '-v' after the command name is --verbose, not --version.
        CommandAppResult result = await Tester.RunAsync(
            ["generate", "-o", "keys", "-v", "--plaintext"],
            TestContext.Current.CancellationToken
        );

        // Assert — the command actually runs (verbose), rather than short-circuiting to version.
        Assert.Equal(ExitCodes.Success, result.ExitCode);
    }
}
