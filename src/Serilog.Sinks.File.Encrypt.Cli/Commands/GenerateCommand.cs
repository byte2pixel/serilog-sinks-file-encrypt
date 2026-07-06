using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using Serilog.Sinks.File.Encrypt.Cli.Infrastructure;
using Serilog.Sinks.File.Encrypt.Models;
using Spectre.Console.Cli;

namespace Serilog.Sinks.File.Encrypt.Cli.Commands;

/// <summary>
/// Generates a new RSA public/private key pair and saves them to the specified output path.
/// </summary>
/// <param name="writer">The verbosity-aware console writer.</param>
/// <param name="fileSystem">The file system.</param>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class GenerateCommand(IConsoleWriter writer, IFileSystem fileSystem)
    : Command<GenerateCommand.Settings>
{
    /// <summary>
    /// The settings for the GenerateCommand.
    /// </summary>
    public sealed class Settings : GlobalCommandSettings
    {
        /// <summary>
        /// The output path to write the public/private key pair.
        /// </summary>
        [CommandOption("-o|--output <OUTPUT>", isRequired: true)]
        [Description("The output path to write the public/private key pair")]
        public string OutputPath { get; init; } = string.Empty;

        /// <summary>
        /// The size of the RSA key in bits.
        /// </summary>
        [CommandOption("-k|--key-size <KEY_SIZE>")]
        [Description("The size of the RSA key in bits (default: 2048)")]
        [DefaultValue(2048)]
        public int KeySize { get; init; } = 2048;

        /// <summary>
        /// The export format for the RSA keys.
        /// </summary>
        [CommandOption("-f|--format <FORMAT>")]
        [Description("The encoding format (Xml or Pem) for the RSA keys (default: Xml)")]
        public KeyFormat Format { get; init; } = KeyFormat.Xml;

        /// <summary>
        /// Overwrite existing key files. Without this flag, generation is refused when a
        /// key file already exists — overwriting a private key permanently loses access to
        /// all logs encrypted with it.
        /// </summary>
        [CommandOption("--force")]
        [Description("Overwrite existing key files (default: refuse if key files exist)")]
        [DefaultValue(false)]
        public bool Force { get; init; }
    }

    /// <summary>
    /// Generates a new RSA key pair and writes them to the specified output path.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The generate settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public override int Execute(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken
    )
    {
        writer.Verbosity = settings.Verbosity;
        try
        {
            // Ensure output directory exists
            if (!fileSystem.Directory.Exists(settings.OutputPath))
            {
                fileSystem.Directory.CreateDirectory(settings.OutputPath);
                writer.Info($"[yellow]Created directory:[/] {settings.OutputPath}");
            }

            string fileExt = settings.Format.ToString().ToLower();

            // Define file paths
            string privateKeyPath = fileSystem.Path.Join(
                settings.OutputPath,
                $"private_key.{fileExt}"
            );
            string publicKeyPath = fileSystem.Path.Join(
                settings.OutputPath,
                $"public_key.{fileExt}"
            );

            if (!settings.Force)
            {
                string[] existing = new[] { privateKeyPath, publicKeyPath }
                    .Where(fileSystem.File.Exists)
                    .ToArray();
                if (existing.Length > 0)
                {
                    writer.Error(
                        $"[red]✗ Refused: key file(s) already exist: {string.Join(", ", existing)}[/]"
                    );
                    writer.Warning(
                        $"[yellow]Use --force to overwrite. Overwriting a private key permanently loses access to all logs encrypted with it.[/]"
                    );
                    return ExitCodes.UsageError;
                }
            }

            // Generate the RSA key pair
            (string publicKey, string privateKey) keyPair = CryptographicUtils.GenerateRsaKeyPair(
                settings.KeySize,
                settings.Format
            );

            // Write keys to files
            fileSystem.File.WriteAllText(privateKeyPath, keyPair.privateKey);
            fileSystem.File.WriteAllText(publicKeyPath, keyPair.publicKey);

            // Output success message
            writer.Info($"[green]✓ Successfully generated RSA key pair![/]");
            writer.BlankLine();
            writer.Info($"[red]Private Key:[/] {privateKeyPath}");
            writer.Info($"[yellow]Public Key:[/] {publicKeyPath}");
            writer.BlankLine();
            writer.Warning($"[yellow]⚠️  Keep your private key secure and never share it![/]");

            return ExitCodes.Success;
        }
        catch (IOException ex)
        {
            writer.Error($"[red]Error writing key files: {ex.Message}[/]");
            return ExitCodes.RuntimeFailure;
        }
        catch (UnauthorizedAccessException ex)
        {
            writer.Error($"[red]Access denied to output path: {ex.Message}[/]");
            return ExitCodes.RuntimeFailure;
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            writer.Error($"[red]Error generating RSA key pair: {ex.Message}[/]");
            return ExitCodes.RuntimeFailure;
        }
    }
}
