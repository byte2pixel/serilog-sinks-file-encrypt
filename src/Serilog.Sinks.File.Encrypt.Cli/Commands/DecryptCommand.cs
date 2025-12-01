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
        /// The path to an encrypted log file or directory containing encrypted log files.
        /// Supports glob patterns like *.log
        /// </summary>
        [CommandArgument(0, "<PATH>")]
        [Description("Path to encrypted log file, directory, or glob pattern (e.g., *.log)")]
        public string InputPath { get; init; } = string.Empty;

        /// <summary>
        /// The path to the RSA private key file in XML format.
        /// </summary>
        [CommandOption("-k|--key <KEY>")]
        [Description("The file containing the RSA private key in XML format")]
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
        /// File pattern to match when processing directories (e.g., *.log, app*.txt).
        /// </summary>
        [CommandOption("-p|--pattern <PATTERN>")]
        [Description("File pattern to match when processing directories (default: *.log)")]
        [DefaultValue("*.log")]
        public string Pattern { get; init; } = "*.log";

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
            if (ValidateInputs(settings))
            {
                return 1;
            }

            string rsaPrivateKey = await fileSystem.File.ReadAllTextAsync(
                settings.KeyFile,
                cancellationToken
            );

            // Determine if input is a file, directory, or pattern
            List<string> filesToDecrypt = GetFilesToDecrypt(settings);

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

            console.WriteLine();

            (int successCount, int failureCount) = await ProcessFilesAsync(
                settings,
                filesToDecrypt,
                rsaPrivateKey,
                streamingOptions,
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

    /// <summary>
    /// Loops through and processes each file for decryption.
    /// </summary>
    /// <param name="settings">The command settings.</param>
    /// <param name="filesToDecrypt">The list of files to decrypt</param>
    /// <param name="rsaPrivateKey">The private key.</param>
    /// <param name="streamingOptions">The streaming decryption options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    private async Task<(int successCount, int failureCount)> ProcessFilesAsync(
        Settings settings,
        List<string> filesToDecrypt,
        string rsaPrivateKey,
        StreamingOptions streamingOptions,
        CancellationToken cancellationToken
    )
    {
        int successCount = 0;
        int failureCount = 0;
        foreach (string inputFile in filesToDecrypt)
        {
            string outputFile = DetermineOutputPath(inputFile, settings);

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

                await EncryptionUtils.DecryptLogFileAsync(
                    inputStream,
                    outputStream,
                    rsaPrivateKey,
                    streamingOptions,
                    cancellationToken
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
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                console.MarkupLineInterpolated(
                    $"[red]✗ Decryption failed:[/] {inputFile} - {ex.Message}"
                );
                failureCount++;
            }
        }

        return (successCount, failureCount);
    }

    /// <summary>
    /// Simple input validation of the settings.
    /// </summary>
    /// <param name="settings">The command settings.</param>
    /// <returns>True if all the inputs pass validation.</returns>
    private bool ValidateInputs(Settings settings)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(settings.InputPath))
        {
            console.MarkupLine("[red]✗ Error: Input path is required.[/]");
            return true;
        }

        if (!fileSystem.File.Exists(settings.KeyFile))
        {
            console.MarkupLineInterpolated(
                $"[red]✗ Error: Key file '{settings.KeyFile}' does not exist.[/]"
            );
            return true;
        }

        console.MarkupLineInterpolated($"[blue]Reading private key from:[/] {settings.KeyFile}");
        return false;
    }

    /// <summary>
    /// Gets the list of files to decrypt based on the input path and settings.
    /// </summary>
    /// <param name="settings">The command settings.</param>
    private List<string> GetFilesToDecrypt(Settings settings)
    {
        List<string> files = [];
        string[] foundFiles;
        SearchOption searchOption;

        // Check if it's a direct file path
        if (fileSystem.File.Exists(settings.InputPath))
        {
            files.Add(settings.InputPath);
            return files;
        }

        // Check if it's a directory
        if (fileSystem.Directory.Exists(settings.InputPath))
        {
            searchOption = settings.Recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            foundFiles = fileSystem.Directory.GetFiles(
                settings.InputPath,
                settings.Pattern,
                searchOption
            );
            files.AddRange(foundFiles);
            return files;
        }

        if (!settings.InputPath.Contains('*') && !settings.InputPath.Contains('?'))
        {
            return files;
        }

        string? directory = fileSystem.Path.GetDirectoryName(settings.InputPath);
        string pattern = fileSystem.Path.GetFileName(settings.InputPath);

        if (string.IsNullOrEmpty(directory))
        {
            directory = fileSystem.Directory.GetCurrentDirectory();
        }

        if (!fileSystem.Directory.Exists(directory) || string.IsNullOrEmpty(pattern))
        {
            return files;
        }

        searchOption = settings.Recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        foundFiles = fileSystem.Directory.GetFiles(directory, pattern, searchOption);
        files.AddRange(foundFiles);

        return files;
    }

    /// <summary>
    /// Determines the output file path based on the input file and settings.
    /// </summary>
    /// <param name="inputFile">The log file that is being decrypted.</param>
    /// <param name="settings">The command settings.</param>
    private string DetermineOutputPath(string inputFile, Settings settings)
    {
        // If output path is explicitly specified
        if (!string.IsNullOrWhiteSpace(settings.OutputPath))
        {
            if (!fileSystem.Directory.Exists(settings.OutputPath))
            {
                return settings.OutputPath;
            }

            string inputFileName = fileSystem.Path.GetFileName(inputFile);
            string outputFileName = GenerateDecryptedFileName(inputFileName);
            return fileSystem.Path.Combine(settings.OutputPath, outputFileName);
        }

        // Default: add .decrypted extension in the same directory
        string directory = fileSystem.Path.GetDirectoryName(inputFile) ?? string.Empty;
        string inputFileName2 = fileSystem.Path.GetFileName(inputFile);
        string decryptedFileName = GenerateDecryptedFileName(inputFileName2);

        return fileSystem.Path.Combine(directory, decryptedFileName);
    }

    /// <summary>
    /// Generates a decrypted filename by adding .decrypted before the extension.
    /// Example: app.log -> app.decrypted.log
    /// </summary>
    /// <param name="fileName">The name of the encrypted log file.</param>
    /// <returns>The name to use for the decrypted log file.</returns>
    private string GenerateDecryptedFileName(string fileName)
    {
        string nameWithoutExtension = fileSystem.Path.GetFileNameWithoutExtension(fileName);
        string extension = fileSystem.Path.GetExtension(fileName);

        return $"{nameWithoutExtension}.decrypted{extension}";
    }
}
