using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Security.Cryptography;
using Serilog.Sinks.File.Decrypt;
using Serilog.Sinks.File.Decrypt.Models;
using Serilog.Sinks.File.Encrypt.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Serilog.Sinks.File.Encrypt.Cli.Commands;

/// <summary>
/// Command to decrypt an encrypted log file using an RSA private key
/// </summary>
/// <param name="writer">The verbosity-aware console writer</param>
/// <param name="fileSystem">The file system</param>
/// <param name="inputResolver">The service that resolves file paths from input (supports files, directories, and glob patterns)</param>
/// <param name="outputResolver">The service that resolves the output path where the decrypted file will be saved.</param>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class DecryptCommand(
    IConsoleWriter writer,
    IFileSystem fileSystem,
    IInputResolver inputResolver,
    IOutputResolver outputResolver
) : AsyncCommand<DecryptCommand.Settings>
{
    /// <summary>
    /// The settings for the DecryptCommand
    /// </summary>
    public sealed class Settings : GlobalCommandSettings
    {
        /// <summary>
        /// The path to an encrypted log file or directory containing encrypted log files.
        /// </summary>
        [CommandArgument(0, "<PATH>")]
        [Description(
            @"Path to encrypted log file, for a directory append <PATH>\*.log or <PATH>\*.txt (Not recursive)"
        )]
        public string InputPath { get; init; } = string.Empty;

        /// <summary>
        /// The path to the RSA private key file in XML or PEM format.
        /// </summary>
        [CommandOption("-k|--key <KEY>")]
        [Description("The file containing the RSA private key in XML or PEM format")]
        public string KeyFile { get; init; } = "private_key.xml";

        /// <summary>
        /// The id of the key to use for decryption. This should match exactly the key id used during encryption.
        /// If not specified, the default key id of "" (empty string) will be used.
        /// The CLI currently only supports decrypting with a single key. If you need multiple keys
        /// use the underlying library directly in your own application.
        /// </summary>
        [CommandOption("--id")]
        [Description("The id of the key to use for decryption.(default: \"\")")]
        public string KeyId { get; init; } = string.Empty;

        /// <summary>
        /// The output directory or file path. If not specified, files are decrypted in place with .decrypted extension.
        /// </summary>
        [CommandOption("-o|--output <OUTPUT>")]
        [Description(
            "Output directory or file path (default: adds .decrypted to original filename)"
        )]
        public string? OutputPath { get; init; }

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
        /// Treat any session that is not cryptographically verified as sealed as an error.
        /// Combined with --strict, an unsealed (crashed or truncated) or v1-format session
        /// fails the file instead of only being reported.
        /// </summary>
        [CommandOption("--require-sealed")]
        [Description(
            "Treat sessions without a verified end-of-log seal (crashed, truncated, or v1-format) as errors. Combine with --strict to fail the file."
        )]
        [DefaultValue(false)]
        public bool RequireSealed { get; init; }

        /// <summary>
        /// Path to write detailed audit information. If not specified, a temporary file will be used.
        /// </summary>
        [CommandOption("--audit-log <PATH>")]
        [Description(
            "Write detailed audit information to a separate log file. Rolls on 10 MB with up to 7 retained files."
        )]
        public string? AuditLogPath { get; init; }
    }

    private bool IsFile { get; set; }
    private bool IsGlobPattern { get; set; }
    private bool IsValidInput => IsFile || IsGlobPattern;

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

        if (fileSystem.Directory.Exists(settings.InputPath))
        {
            return ValidationResult.Error(
                "✗ Error: Input path cannot be a directory. Please specify a file or add a pattern (<PATH>\\*.log) to select files within the directory."
            );
        }

        // Check if settings.InputPath is a valid file or directory with pattern
        IsFile = fileSystem.File.Exists(settings.InputPath);
        IsGlobPattern = settings.InputPath.Contains('*') || settings.InputPath.Contains('?');
        if (!IsValidInput)
        {
            return ValidationResult.Error(
                $"✗ Error: Input path '{settings.InputPath}' is not a valid file or directory with pattern."
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
        writer.Verbosity = settings.Verbosity;
        try
        {
            writer.Info($"[blue]Reading private key from:[/] {settings.KeyFile}");
            string rsaPrivateKey = await fileSystem.File.ReadAllTextAsync(
                settings.KeyFile,
                cancellationToken
            );

            IReadOnlyList<string> filesToDecrypt = inputResolver.ResolveFiles(settings.InputPath);

            if (filesToDecrypt.Count == 0)
            {
                writer.Warning(
                    $"[yellow]⚠ No files found matching the specified path or pattern.[/]"
                );
                return ExitCodes.NoFilesMatched;
            }

            writer.Info($"[blue]Found {filesToDecrypt.Count} file(s) to decrypt[/]");

            // Configure streaming options based on user settings
            ErrorHandlingMode errorMode = settings.Strict
                ? ErrorHandlingMode.ThrowException
                : ErrorHandlingMode.Skip;

            string auditLog =
                settings.AuditLogPath
                ?? Path.Join(Path.GetTempPath(), Path.GetRandomFileName() + ".log");
            writer.Info($"[dim]Audit log:[/] {auditLog}");
            _logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(
                    auditLog,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB
                    shared: true,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: 7
                )
                .CreateLogger();

            LocalKeyProvider keyProvider = new(settings.KeyId, rsaPrivateKey);

            DecryptionOptions decryptionOptions = new()
            {
                KeyProvider = keyProvider,
                ErrorHandlingMode = errorMode,
                RequireSealed = settings.RequireSealed,
            };

            writer.BlankLine();

            (int successCount, int failureCount, int zeroOutputCount) = await ProcessFilesAsync(
                settings,
                filesToDecrypt,
                decryptionOptions,
                cancellationToken
            );

            // Summary
            writer.BlankLine();
            writer.Info($"[bold]Summary:[/]");
            writer.Info($"  [green]✓ Success:[/] {successCount}");
            if (failureCount > 0)
            {
                writer.Warning($"  [red]✗ Failed:[/] {failureCount}");
            }
            if (zeroOutputCount > 0)
            {
                writer.Warning($"  [yellow]⚠ Nothing decrypted:[/] {zeroOutputCount}");
            }

            await Log.CloseAndFlushAsync();
            if (failureCount > 0)
            {
                return ExitCodes.RuntimeFailure;
            }
            return zeroOutputCount > 0 ? ExitCodes.NothingDecrypted : ExitCodes.Success;
        }
        catch (IOException ex)
        {
            writer.Error($"[red]✗ Error reading or writing files: {ex.Message}[/]");
            return ExitCodes.RuntimeFailure;
        }
        catch (UnauthorizedAccessException ex)
        {
            writer.Error($"[red]✗ Access denied: {ex.Message}[/]");
            return ExitCodes.RuntimeFailure;
        }
        catch (CryptographicException ex)
        {
            writer.Error($"[red]✗ Decryption failed: {ex.Message}[/]");
            return ExitCodes.RuntimeFailure;
        }
        catch (FormatException ex)
        {
            writer.Error($"[red]✗ Invalid key or file format: {ex.Message}[/]");
            return ExitCodes.RuntimeFailure;
        }
        catch (InvalidOperationException ex)
        {
            writer.Error($"[red]✗ Invalid file: {ex.Message}[/]");
            return ExitCodes.RuntimeFailure;
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
    private async Task<(int successCount, int failureCount, int zeroOutputCount)> ProcessFilesAsync(
        Settings settings,
        IReadOnlyList<string> filesToDecrypt,
        DecryptionOptions decryptionOptions,
        CancellationToken cancellationToken
    )
    {
        int successCount = 0;
        int failureCount = 0;
        int zeroOutputCount = 0;
        foreach (string inputFile in filesToDecrypt)
        {
            string outputFile = outputResolver.ResolveOutputPath(
                inputFile,
                settings.InputPath,
                settings.OutputPath
            );

            if (fileSystem.File.Exists(outputFile))
            {
                writer.Info($"[dim]{outputFile} will be overwritten.[/]");
            }

            try
            {
                writer.Info($"[cyan]⧗ Decrypting:[/] {inputFile}");

                string? outputDir = fileSystem.Path.GetDirectoryName(outputFile);
                if (!string.IsNullOrEmpty(outputDir) && !fileSystem.Directory.Exists(outputDir))
                {
                    fileSystem.Directory.CreateDirectory(outputDir);
                }

                // Perform the decryption using streaming API
                DecryptionResult result;
                await using (FileSystemStream inputStream = fileSystem.File.OpenRead(inputFile))
                await using (FileSystemStream outputStream = fileSystem.File.Create(outputFile))
                {
                    result = await DecryptionUtils.DecryptLogFileAsync(
                        inputStream,
                        outputStream,
                        decryptionOptions,
                        _logger,
                        cancellationToken: cancellationToken
                    );
                }

                if (result.NothingDecrypted)
                {
                    writer.Warning(
                        $"[yellow]⚠ Nothing decrypted:[/] {inputFile} — no sessions could be decrypted (wrong key, wrong --id, or not an encrypted log)"
                    );
                    _logger?.Warning(
                        "Nothing decrypted from {InputFile}: {FailedHeaders} failed header(s), {FailedMessages} failed message(s). Wrong key, wrong key id, or unrecognized file format.",
                        inputFile,
                        result.FailedHeaders,
                        result.FailedMessages
                    );
                    fileSystem.File.Delete(outputFile);
                    writer.Info($"  [dim]Removed empty output file {outputFile}[/]");
                    zeroOutputCount++;
                    continue;
                }

                if (result.FailedHeaders > 0 || result.FailedMessages > 0)
                {
                    writer.Warning(
                        $"[yellow]⚠ Decryption completed with {result.FailedHeaders} failed headers and {result.FailedMessages} failed messages.\nCheck audit log.[/]"
                    );
                }
                else
                {
                    writer.Info($"[green]✓ Decrypted:[/] {inputFile}");
                }

                writer.Verbose(
                    $"  [dim]sessions: {result.DecryptedSessions}, messages: {result.DecryptedMessages}, failed headers: {result.FailedHeaders}, failed messages: {result.FailedMessages}, resync attempts: {result.ResyncAttempts}[/]"
                );

                ReportSealStatus(result);

                writer.Info($"  [dim]→ {outputFile}[/]");
                successCount++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                writer.Error($"[red]✗ Failed:[/] {inputFile} - {ex.Message}");
                failureCount++;
            }
            catch (CryptographicException ex)
            {
                writer.Error($"[red]✗ Decryption failed:[/] {inputFile} - {ex.Message}");
                failureCount++;
            }
        }

        return (successCount, failureCount, zeroOutputCount);
    }

    /// <summary>
    /// Reports the end-of-log seal verification status of each decrypted session, so the operator
    /// can tell verified-complete logs apart from crashed, truncated, or tampered ones.
    /// </summary>
    /// <param name="result">The decryption result to report on.</param>
    private void ReportSealStatus(DecryptionResult result)
    {
        foreach (SessionResult session in result.Sessions)
        {
            switch (session.SealStatus)
            {
                case SealStatus.Sealed:
                    _logger?.Information(
                        "Session {Index}: sealed and complete ({Messages} message(s)).",
                        session.Index,
                        session.DecryptedMessages
                    );
                    break;
                case SealStatus.NotApplicable:
                    writer.Info(
                        $"  [dim]• Session {session.Index} (v1): legacy format, completeness cannot be verified[/]"
                    );
                    _logger?.Information(
                        "Session {Index}: v1 format, no seal support.",
                        session.Index
                    );
                    break;
                case SealStatus.Unsealed:
                    writer.Warning(
                        $"  [yellow]⚠ Session {session.Index}: UNSEALED — the log was truncated or the application did not close cleanly[/]"
                    );
                    _logger?.Warning(
                        "Session {Index}: unsealed (crash or truncation; {Messages} message(s) recovered).",
                        session.Index,
                        session.DecryptedMessages
                    );
                    break;
                case SealStatus.SealCountMismatch:
                    writer.Error(
                        $"  [red]✗ Session {session.Index}: seal count mismatch — seal declares {session.DeclaredFrameCount} frame(s), {session.DecryptedMessages} decrypted (tail truncated)[/]"
                    );
                    _logger?.Error(
                        "Session {Index}: seal declares {Declared} frame(s) but {Decrypted} were decrypted — tail truncated.",
                        session.Index,
                        session.DeclaredFrameCount,
                        session.DecryptedMessages
                    );
                    break;
                case SealStatus.SealInvalid:
                    writer.Error(
                        $"  [red]✗ Session {session.Index}: invalid seal — the end of the session was tampered with or corrupted[/]"
                    );
                    _logger?.Error(
                        "Session {Index}: seal record failed verification (tampering or corruption).",
                        session.Index
                    );
                    break;
                default:
                    break;
            }
        }

        if (
            result.Sessions.Count > 0
            && result.Sessions.All(s => s.SealStatus == SealStatus.Sealed)
        )
        {
            writer.Info(
                $"  [green]✓ All {result.Sessions.Count} session(s) sealed and complete[/]"
            );
        }
    }
}
