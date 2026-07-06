using Spectre.Console;

namespace Serilog.Sinks.File.Encrypt.Cli.Tests;

public class GlobalCommandSettingsTests
{
    [Fact]
    public void Validate_QuietAndVerbose_ReturnsError()
    {
        GlobalCommandSettings settings = new() { Quiet = true, Verbose = true };

        ValidationResult result = settings.Validate();

        result.Successful.ShouldBeFalse();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("--quiet and --verbose cannot be combined");
    }

    [Theory]
    [InlineData(false, false, Verbosity.Normal)]
    [InlineData(true, false, Verbosity.Quiet)]
    [InlineData(false, true, Verbosity.Verbose)]
    public void Verbosity_DerivesFromFlags(bool quiet, bool verbose, Verbosity expected)
    {
        GlobalCommandSettings settings = new() { Quiet = quiet, Verbose = verbose };

        settings.Validate().Successful.ShouldBeTrue();
        settings.Verbosity.ShouldBe(expected);
    }
}
