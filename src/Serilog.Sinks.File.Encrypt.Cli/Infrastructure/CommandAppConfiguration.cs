using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Sinks.File.Encrypt.Cli.Commands;
using Spectre.Console.Cli;

namespace Serilog.Sinks.File.Encrypt.Cli.Infrastructure;

/// <summary>
/// Contains configuration and setup logic for the command-line application
/// </summary>
public static class CommandAppConfiguration
{
    /// <summary>
    /// Creates and configures a TypeRegistrar with all required services
    /// </summary>
    /// <param name="fileSystem">Optional file system implementation for testing. If null, uses the real file system.</param>
    /// <returns>A configured TypeRegistrar instance</returns>
    public static TypeRegistrar CreateRegistrar(IFileSystem? fileSystem = null)
    {
        ServiceCollection registrations = new();
        registrations.AddCliServices(fileSystem);
        return new TypeRegistrar(registrations);
    }

    /// <summary>
    /// Gets the configuration action for the CommandApp
    /// </summary>
    /// <returns>An action that configures the CommandApp</returns>
    public static Action<IConfigurator> GetConfiguration()
    {
        return c =>
        {
            c.SetApplicationName("serilog-encrypt");
            c.AddCommand<GenerateCommand>("generate")
                .WithDescription("Generate a new RSA key pair for log encryption");
            c.AddCommand<DecryptCommand>("decrypt")
                .WithDescription("Decrypt encrypted log files using an RSA private key");
            c.ValidateExamples();
        };
    }
}
