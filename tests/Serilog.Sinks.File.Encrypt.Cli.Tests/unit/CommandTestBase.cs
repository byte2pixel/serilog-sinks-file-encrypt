namespace Serilog.Sinks.File.Encrypt.Cli.Tests.unit;

/// <summary>
/// Base class for CLI command tests that provides common test infrastructure
/// including TestConsole management and disposal.
/// </summary>
public abstract class CommandTestBase : IDisposable
{
    protected static readonly string[] Arguments = [];
    protected static readonly IRemainingArguments Remaining = Substitute.For<IRemainingArguments>();

    protected TestConsole TestConsole { get; } = new();
    protected MockFileSystem FileSystem { get; } = new();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            TestConsole.Dispose();
        }
    }
}
