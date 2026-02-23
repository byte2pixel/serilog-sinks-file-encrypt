using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Serilog.Sinks.File.Encrypt.Cli.Commands;

/// <summary>
/// Command to decrypt an encrypted log file using an RSA private key
/// </summary>
/// <param name="console">The ANSI console</param>
/// <param name="fileSystem">The file system</param>
/// <param name="inputResolver">The service that resolves file paths from input (supports files, directories, and glob patterns)</param>
/// <param name="outputResolver">The service that resolves the output path where the decrypted file will be saved.</param>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class DecryptCommand(
    IAnsiConsole console,
    IFileSystem fileSystem,
    IInputResolver inputResolver,
    IOutputResolver outputResolver
) : AsyncCommand<DecryptCommand.Settings>
{
    /// <summary>
    /// The settings for the DecryptCommand
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// The path to an encrypted log file or directory containing encrypted log files.
        /// Supports glob patterns like *.log
        /// </summary>
        [CommandArgument(0, "<PATH>")]
        [Description(
            "Path to encrypted log file, directory (uses *.log pattern), or glob pattern (e.g., *.log)"
        )]
        public string InputPath { get; init; } = string.Empty;

        /// <summary>
        /// The path to the RSA private key file in XML or PEM format.
        /// </summary>
        [CommandOption("-k|--key <KEY>")]
        [Description("The file containing the RSA private key in XML or PEM format")]
        public string KeyFile { get; init; } = "private_key.xml";

        /// <summary>
        /// The output directory or file path. If not specified, files are decrypted in place with .decrypted extension.
        /// </summary>
        [CommandOption("-o|--output <OUTPUT>")]
        [Description(
            "Output directory or file path (default: adds .decrypted to original filename)"
        )]
        public string? OutputPath { get; init; }

        /// <summary>
        /// Process directories recursively.
        /// </summary>
        [CommandOption("-r|--recursive")]
        [Description("Process directories recursively")]
        [DefaultValue(false)]
        public bool Recursive { get; init; }

        /// <summary>
        /// Fail immediately on first decryption error instead of continuing with remaining files.
        /// </summary>
        [CommandOption("-s|--strict")]
        [Description(
            "Fail immediately on first decryption error (default: false, continues processing)"
        )]
        [DefaultValue(false)]
        public bool Strict { get; init; }

        /// <summary>
        /// Path to write detailed audit information. If specified, audit info is logged to this file instead of being skipped silently.
        /// </summary>
        [CommandOption("--audit-log <PATH>")]
        [Description(
            "Write detailed audit information to a separate log file. Rolls on 10 MB with up to 7 retained files."
        )]
        public string? AuditLogPath { get; init; }
    }

    private bool IsFile { get; set; }
    private bool IsDirectory { get; set; }
    private bool IsGlobPattern { get; set; }
    private bool IsValidInput => IsFile || IsDirectory || IsGlobPattern;

    private ILogger? _logger;

    /// <inheritdoc />
    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(settings.InputPath))
        {
            return ValidationResult.Error("✗ Error: Input path is required.");
        }

        if (!fileSystem.File.Exists(settings.KeyFile))
        {
            return ValidationResult.Error(
                $"✗ Error: Key file '{settings.KeyFile}' does not exist."
            );
        }

        // Check if settings.InputPath is a valid file, directory, or glob pattern
        IsFile = fileSystem.File.Exists(settings.InputPath);
        IsDirectory = fileSystem.Directory.Exists(settings.InputPath);
        IsGlobPattern = settings.InputPath.Contains('*') || settings.InputPath.Contains('?');
        if (!IsValidInput)
        {
            return ValidationResult.Error(
                $"✗ Error: Input path '{settings.InputPath}' is not a valid file, directory, or glob pattern."
            );
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Decrypts the specified encrypted log file(s) using the provided RSA private key and writes the decrypted content to output file(s).
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
            console.MarkupLineInterpolated(
                $"[blue]Reading private key from:[/] {settings.KeyFile}"
            );
            string rsaPrivateKey = await fileSystem.File.ReadAllTextAsync(
                settings.KeyFile,
                cancellationToken
            );

            // Determine if input is a file, directory, or pattern
            IReadOnlyList<string> filesToDecrypt = inputResolver.ResolveFiles(
                settings.InputPath,
                settings.Recursive
            );

            if (filesToDecrypt.Count == 0)
            {
                console.MarkupLineInterpolated(
                    $"[yellow]⚠ No files found matching the specified path or pattern.[/]"
                );
                return 0;
            }

            console.MarkupLineInterpolated(
                $"[blue]Found {filesToDecrypt.Count} file(s) to decrypt[/]"
            );

            // Configure streaming options based on user settings
            ErrorHandlingMode errorMode = settings.Strict
                ? ErrorHandlingMode.ThrowException
                : ErrorHandlingMode.Skip;

            if (!string.IsNullOrWhiteSpace(settings.AuditLogPath))
            {
                console.MarkupLineInterpolated($"[dim]Audit log:[/] {settings.AuditLogPath}");
                _logger = new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .WriteTo.File(
                        settings.AuditLogPath,
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                        fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB
                        shared: true,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: 7
                    )
                    .CreateLogger();
            }

            // TODO: need a way to support multiple keys with key IDs for rotation scenarios.
            // For now, we will just use a single default key with an empty key ID since the CLI only accepts one key file.
            var decryptionKeys = new Dictionary<string, string>
            {
                { "", rsaPrivateKey }, // Default key with empty key ID
            };

            DecryptionOptions decryptionOptions = new()
            {
                DecryptionKeys = decryptionKeys,
                ErrorHandlingMode = errorMode,
            };

            console.WriteLine();

            (int successCount, int failureCount) = await ProcessFilesAsync(
                settings,
                filesToDecrypt,
                decryptionOptions,
                cancellationToken
            );

            // Summary
            console.WriteLine();
            console.MarkupLine("[bold]Summary:[/]");
            console.MarkupLineInterpolated($"  [green]✓ Success:[/] {successCount}");
            if (failureCount > 0)
            {
                console.MarkupLineInterpolated($"  [red]✗ Failed:[/] {failureCount}");
            }

            await Log.CloseAndFlushAsync();
            return failureCount > 0 ? 1 : 0;
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
        catch (CryptographicException ex)
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

    /// <summary>
    /// Loops through and processes each file for decryption.
    /// </summary>
    /// <param name="settings">The command settings.</param>
    /// <param name="filesToDecrypt">The list of files to decrypt</param>
    /// <param name="decryptionOptions">The decryption options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    private async Task<(int successCount, int failureCount)> ProcessFilesAsync(
        Settings settings,
        IReadOnlyList<string> filesToDecrypt,
        DecryptionOptions decryptionOptions,
        CancellationToken cancellationToken
    )
    {
        int successCount = 0;
        int failureCount = 0;
        foreach (string inputFile in filesToDecrypt)
        {
            string outputFile = outputResolver.ResolveOutputPath(inputFile, settings.InputPath, settings.OutputPath);

            if (fileSystem.File.Exists(outputFile))
            {
                console.MarkupLineInterpolated($"[dim]{outputFile} will be overwritten.[/]");
            }

            try
            {
                console.MarkupLineInterpolated($"[cyan]⧗ Decrypting:[/] {inputFile}");

                string? outputDir = fileSystem.Path.GetDirectoryName(outputFile);
                if (!string.IsNullOrEmpty(outputDir) && !fileSystem.Directory.Exists(outputDir))
                {
                    fileSystem.Directory.CreateDirectory(outputDir);
                }

                // Perform the decryption using streaming API
                await using FileSystemStream inputStream = fileSystem.File.OpenRead(inputFile);
                await using FileSystemStream outputStream = fileSystem.File.Create(outputFile);
                await CryptographicUtils.DecryptLogFileAsync(
                    inputStream,
                    outputStream,
                    decryptionOptions,
                    _logger,
                    cancellationToken: cancellationToken
                );

                console.MarkupLineInterpolated($"[green]✓ Decrypted:[/] {inputFile}");
                console.MarkupLineInterpolated($"  [dim]→ {outputFile}[/]");
                successCount++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                console.MarkupLineInterpolated($"[red]✗ Failed:[/] {inputFile} - {ex.Message}");
                failureCount++;
            }
            catch (CryptographicException ex)
            {
                console.MarkupLineInterpolated(
                    $"[red]✗ Decryption failed:[/] {inputFile} - {ex.Message}"
                );
                failureCount++;
            }
        }

        return (successCount, failureCount);
    }
}
