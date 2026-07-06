namespace Serilog.Sinks.File.Encrypt.Cli.Tests;

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

    /// <summary>
    /// A real ConsoleWriter wrapping <see cref="TestConsole"/>, so command output
    /// assertions keep working against <c>TestConsole.Output</c>.
    /// </summary>
    protected ConsoleWriter Writer { get; }

    protected CommandTestBase()
    {
        Writer = new ConsoleWriter(TestConsole);
    }

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
