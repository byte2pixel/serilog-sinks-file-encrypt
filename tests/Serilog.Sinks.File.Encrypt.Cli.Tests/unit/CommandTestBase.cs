namespace Serilog.Sinks.File.Encrypt.Cli.Tests.unit;

/// <summary>
/// Base class for CLI command tests that provides common test infrastructure
/// including TestConsole management and disposal.
/// </summary>
public abstract class CommandTestBase : IDisposable
{
    protected static readonly string[] Arguments = [];
    protected static readonly IRemainingArguments Remaining = Substitute.For<IRemainingArguments>();

    protected readonly TestConsole TestConsole = new();
    protected readonly MockFileSystem FileSystem = new();
    protected readonly IFileSystem FileSystemSub = Substitute.For<IFileSystem>();

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
