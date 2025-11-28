using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using Serilog.Sinks.File.Encrypt.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Serilog.Sinks.File.Encrypt.Cli.Commands;

/// <summary>
/// Command to decrypt an encrypted log file using an RSA private key
/// </summary>
/// <param name="console">The ANSI console</param>
/// <param name="fileSystem">The file system</param>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class DecryptCommand(IAnsiConsole console, IFileSystem fileSystem)
    : AsyncCommand<DecryptCommand.Settings>
{
    /// <summary>
    /// The settings for the DecryptCommand
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// The path to the RSA private key file in XML format.
        /// </summary>
        [CommandOption("-k|--key <KEY>")]
        [Description("The file containing the RSA private key in XML format")]
        public string KeyFile { get; init; } = "privateKey.xml";

        /// <summary>
        /// The path to the encrypted log file to decrypt.
        /// </summary>
        [CommandOption("-f|--file <FILE>")]
        [Description("The encrypted log file to decrypt")]
        public string EncryptedFile { get; init; } = "log.encrypted.txt";

        /// <summary>
        /// The path where the decrypted log content will be saved.
        /// </summary>
        [CommandOption("-o|--output <OUTPUT>")]
        [Description("The output file for the decrypted log content")]
        public string OutputFile { get; init; } = "log.decrypted.txt";

        /// <summary>
        /// How to handle decryption errors (Skip, WriteInline, WriteToErrorLog, ThrowException).
        /// </summary>
        [CommandOption("-e|--error-mode <MODE>")]
        [Description(
            "Error handling mode: Skip (default, clean output), WriteInline (errors inline), WriteToErrorLog (separate file), ThrowException (fail fast)"
        )]
        public ErrorHandlingMode ErrorMode { get; init; } = ErrorHandlingMode.Skip;

        /// <summary>
        /// Path to write error log when using WriteToErrorLog mode. Auto-generated if not specified.
        /// </summary>
        [CommandOption("--error-log <PATH>")]
        [Description("Path for error log file (only used with WriteToErrorLog mode)")]
        public string? ErrorLogPath { get; init; }

        /// <summary>
        /// Whether to continue processing after encountering errors (only applies when not using ThrowException mode).
        /// </summary>
        [CommandOption("--continue-on-error")]
        [Description("Continue decryption even when errors are encountered (default: true)")]
        [DefaultValue(true)]
        public bool ContinueOnError { get; init; } = true;
    }

    /// <summary>
    /// Decrypts the specified encrypted log file using the provided RSA private key and writes the decrypted content to the output file.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The decrypt settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // Validate input files exist
            if (!fileSystem.File.Exists(settings.KeyFile))
            {
                console.MarkupLineInterpolated(
                    $"[red]✗ Error: Key file '{settings.KeyFile}' does not exist.[/]"
                );
                return 1;
            }

            if (!fileSystem.File.Exists(settings.EncryptedFile))
            {
                console.MarkupLineInterpolated(
                    $"[red]✗ Error: Encrypted file '{settings.EncryptedFile}' does not exist.[/]"
                );
                return 1;
            }

            console.MarkupLineInterpolated(
                $"[blue]Reading private key from:[/] {settings.KeyFile}"
            );
            string rsaPrivateKey = await fileSystem.File.ReadAllTextAsync(
                settings.KeyFile,
                cancellationToken
            );

            console.MarkupLineInterpolated(
                $"[blue]Decrypting log file:[/] {settings.EncryptedFile}"
            );

            // Configure streaming options based on user settings
            StreamingOptions streamingOptions = new()
            {
                ErrorHandlingMode = settings.ErrorMode,
                ErrorLogPath = settings.ErrorLogPath,
                ContinueOnError = settings.ContinueOnError,
            };

            // Display error handling configuration
            console.MarkupLineInterpolated($"[dim]Error handling mode:[/] {settings.ErrorMode}");

            if (
                settings.ErrorMode == ErrorHandlingMode.WriteToErrorLog
                && !string.IsNullOrWhiteSpace(settings.ErrorLogPath)
            )
            {
                console.MarkupLineInterpolated($"[dim]Error log path:[/] {settings.ErrorLogPath}");
            }

            // Perform the decryption using streaming API for better memory efficiency
            // Use IFileSystem to open streams so we can mock them in tests
            await using FileSystemStream inputStream = fileSystem.File.OpenRead(
                settings.EncryptedFile
            );
            await using FileSystemStream outputStream = fileSystem.File.Create(settings.OutputFile);

            await EncryptionUtils.DecryptLogFileAsync(
                inputStream,
                outputStream,
                rsaPrivateKey,
                streamingOptions,
                cancellationToken
            );

            console.MarkupLine("[green]✓ Successfully decrypted log file![/]");
            console.MarkupLineInterpolated(
                $"[yellow]Decrypted content written to:[/] {settings.OutputFile}"
            );

            return 0;
        }
        catch (IOException ex)
        {
            console.MarkupLineInterpolated(
                $"[red]✗ Error reading or writing files: {ex.Message}[/]"
            );
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            console.MarkupLineInterpolated($"[red]✗ Access denied: {ex.Message}[/]");
            return 1;
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            console.MarkupLineInterpolated($"[red]✗ Decryption failed: {ex.Message}[/]");
            return 1;
        }
        catch (FormatException ex)
        {
            console.MarkupLineInterpolated($"[red]✗ Invalid key or file format: {ex.Message}[/]");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            console.MarkupLineInterpolated($"[red]✗ Invalid file: {ex.Message}[/]");
            return 1;
        }
    }
}
