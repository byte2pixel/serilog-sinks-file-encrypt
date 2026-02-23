namespace Serilog.Sinks.File.Encrypt.Cli;

/// <summary>
/// Service interface for resolving output file paths during decryption.
/// Handles the difference between single-file inputs (where the output path can be a file or directory)
/// and multi-file inputs such as directories or glob patterns (where the output path is always treated as a directory).
/// </summary>
public interface IOutputResolver
{
    /// <summary>
    /// Resolves the output file path for a given input file.
    /// </summary>
    /// <param name="inputFile">The specific input file being processed.</param>
    /// <param name="inputPath">The original input path supplied by the user.
    /// Used to determine whether the operation is single-file or multi-file.</param>
    /// <param name="outputPath">
    /// The output path specified by the user, or <see langword="null"/> to use the default behavior
    /// (append <c>.decrypted</c> before the extension in the same directory as the input file).
    /// </param>
    /// <returns>The resolved absolute-or-relative output file path.</returns>
    string ResolveOutputPath(string inputFile, string inputPath, string? outputPath);
}
