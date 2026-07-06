namespace Serilog.Sinks.File.Encrypt.Cli.Tests;

public class ExitCodesTests
{
    [Theory]
    [InlineData(-1, ExitCodes.UsageError)]
    [InlineData(-99, ExitCodes.UsageError)]
    [InlineData(ExitCodes.Success, ExitCodes.Success)]
    [InlineData(ExitCodes.RuntimeFailure, ExitCodes.RuntimeFailure)]
    [InlineData(ExitCodes.NoFilesMatched, ExitCodes.NoFilesMatched)]
    public void Normalize_MapsSpectreResultsToContract(int raw, int expected)
    {
        ExitCodes.Normalize(raw).ShouldBe(expected);
    }
}
