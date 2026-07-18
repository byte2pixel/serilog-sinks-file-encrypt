using System.IO.Abstractions;
using System.Reflection;
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
        const string PrivateKey = "private_key.pem";
        const string Decrypt = "decrypt";
        const string Generate = "generate";

        return c =>
        {
            c.SetApplicationName("serilog-encrypt");
            c.SetApplicationVersion(GetInformationalVersion());
            c.UseStrictParsing();
            c.AddCommand<GenerateCommand>(Generate)
                .WithDescription(
                    "Generate a new RSA key pair for log encryption. The private key is "
                        + "passphrase-encrypted (PKCS#8 PEM) unless --plaintext is passed."
                )
                .WithExample(Generate, "--output", "./keys")
                .WithExample(Generate, "-o", "./keys", "--key-size", "4096")
                .WithExample(Generate, "-o", "./keys", "--passphrase-env", "MY_PASSPHRASE")
                .WithExample(Generate, "-o", "./keys", "--passphrase-file", "passphrase.txt")
                .WithExample(Generate, "-o", "./keys", "-f", "Xml", "--plaintext")
                .WithExample(Generate, "-o", "./keys", "--force")
                .WithExample(Generate, "-o", "./keys", "--quiet");

            c.AddCommand<DecryptCommand>(Decrypt)
                .WithDescription(
                    "Decrypt encrypted log files using an RSA private key. "
                        + "Exit codes: 0 success, 1 runtime failure, 2 usage error or refused overwrite, "
                        + "3 no files matched, 4 nothing decrypted, 5 --require-sealed not met."
                )
                .WithExample(Decrypt, "app.log", "-k", PrivateKey)
                .WithExample(Decrypt, "*.log", "-k", PrivateKey)
                .WithExample(Decrypt, "logs/*.txt", "-k", PrivateKey, "--id", "")
                .WithExample(Decrypt, "app.log", "-k", PrivateKey, "-o", "decrypted.log")
                .WithExample(Decrypt, "app.log", "-k", PrivateKey, "--strict")
                .WithExample(Decrypt, "app.log", "-k", PrivateKey, "--force")
                .WithExample(
                    Decrypt,
                    "app.log",
                    "-k",
                    PrivateKey,
                    "--passphrase-env",
                    "MY_PASSPHRASE"
                )
                .WithExample(Decrypt, "app.log", "-k", PrivateKey, "--json")
                .WithExample(Decrypt, "app.log", "-k", PrivateKey, "--require-sealed")
                .WithExample(Decrypt, "app.log", "-k", PrivateKey, "--verbose")
                .WithExample(Decrypt, "./logs/*.log", "-k", PrivateKey, "--audit-log", "audit.log");
            c.ValidateExamples();
        };
    }

    /// <summary>
    /// Resolves the version shown by <c>--version</c> from the assembly's MinVer-stamped
    /// informational version, dropping any <c>+build</c> metadata. Falls back to "unknown"
    /// when the attribute is absent (e.g. a MinVer-skipped Debug build).
    /// </summary>
    private static string GetInformationalVersion()
    {
        string? informational = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return string.IsNullOrEmpty(informational) ? "unknown" : informational.Split('+')[0];
    }
}
