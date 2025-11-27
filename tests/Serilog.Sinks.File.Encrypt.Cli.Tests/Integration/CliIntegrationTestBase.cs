using System.IO.Abstractions;
using Serilog.Sinks.File.Encrypt.Cli.Infrastructure;
using Spectre.Console.Cli.Testing;

namespace Serilog.Sinks.File.Encrypt.Cli.Tests.Integration;

/// <summary>
/// Example base class for CLI integration tests using CommandAppTester
/// </summary>
public class CliIntegrationTestBase
{
    /// <summary>
    /// Creates a CommandAppTester configured with the same settings as the actual application
    /// </summary>
    /// <param name="fileSystem">Optional file system implementation for testing. If null, uses the real file system.</param>
    /// <returns>A configured CommandAppTester instance</returns>
    protected static CommandAppTester CreateCommandAppTester(IFileSystem? fileSystem = null)
    {
        TypeRegistrar registrar = CommandAppConfiguration.CreateRegistrar(
            fileSystem ?? new MockFileSystem()
        );
        CommandAppTesterSettings settings = new();
        TestConsole console = new TestConsole().Width(int.MaxValue);

        CommandAppTester tester = new(registrar, settings, console);
        tester.Configure(CommandAppConfiguration.GetConfiguration());

        return tester;
    }
}
