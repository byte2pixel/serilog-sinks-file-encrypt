using Serilog.Sinks.File.Encrypt.Cli.Infrastructure;
using Spectre.Console.Cli.Testing;

namespace Serilog.Sinks.File.Encrypt.Cli.Tests.Integration;

/// <summary>
/// Example base class for CLI integration tests using CommandAppTester
/// </summary>
public class CliIntegrationTestBase : IDisposable
{
    private readonly TestConsole _console;

    /// <summary>
    /// Gets the CommandAppTester configured with the same settings as the actual application
    /// </summary>
    protected CommandAppTester Tester { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CliIntegrationTestBase"/> class.
    /// </summary>
    /// <param name="fileSystem">Optional file system implementation for testing. If null, uses the real file system.</param>
    protected CliIntegrationTestBase(IFileSystem? fileSystem = null)
    {
        TypeRegistrar registrar = CommandAppConfiguration.CreateRegistrar(
            fileSystem ?? new MockFileSystem()
        );
        CommandAppTesterSettings settings = new();
        _console = new TestConsole().Width(int.MaxValue);

        Tester = new CommandAppTester(registrar, settings, _console);
        Tester.Configure(CommandAppConfiguration.GetConfiguration());
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
            _console.Dispose();
        }
    }
}
