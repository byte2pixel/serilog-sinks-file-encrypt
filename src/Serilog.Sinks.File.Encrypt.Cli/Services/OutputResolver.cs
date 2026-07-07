using System.IO.Abstractions;

namespace Serilog.Sinks.File.Encrypt.Cli;

/// <inheritdoc />
public class OutputResolver(IFileSystem fileSystem) : IOutputResolver
{
    /// <inheritdoc />
    /// <remarks>
    /// Resolution rules:
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <b>No output path specified:</b> the decrypted file is placed next to the input file,
    ///       with <c>.decrypted</c> inserted before the extension
    ///       (e.g. <c>app.log</c> → <c>app.decrypted.log</c>).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <b>Single-file input (<paramref name="inputPath"/> is an existing file):</b>
    ///       the output path may be either a file path or a directory.
    ///       If the path already exists as a directory, ends with a directory separator,
    ///       or has no file extension (e.g. <c>./Decrypted</c>), it is treated as a directory
    ///       and the computed filename is appended; otherwise it is used as-is as a file path.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <b>Multi-file input (directory or glob pattern):</b>
    ///       the output path is <em>always</em> treated as a directory, regardless of whether it
    ///       currently exists, and the computed filename is always appended to it.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    public string ResolveOutputPath(string inputFile, string inputPath, string? outputPath)
    {
        bool isSingleFile = fileSystem.File.Exists(inputPath);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            // Default: place next to the source file with .decrypted inserted before the extension
            string sourceDir = fileSystem.Path.GetDirectoryName(inputFile) ?? string.Empty;
            string decryptedName = GenerateDecryptedFileName(
                fileSystem.Path.GetFileName(inputFile)
            );
            return fileSystem.Path.Join(sourceDir, decryptedName);
        }

        if (isSingleFile)
        {
            // Single-file mode: output path may be a specific file path or a directory.
            // Treat as a directory if it already exists as one, ends with a separator,
            // or has no file extension (e.g. "./Decrypted" is almost certainly directory intent).
            bool outputIsDirectory =
                fileSystem.Directory.Exists(outputPath)
                || outputPath.EndsWith(fileSystem.Path.DirectorySeparatorChar)
                || outputPath.EndsWith(fileSystem.Path.AltDirectorySeparatorChar)
                || string.IsNullOrEmpty(fileSystem.Path.GetExtension(outputPath));

            if (outputIsDirectory)
            {
                string decryptedName = GenerateDecryptedFileName(
                    fileSystem.Path.GetFileName(inputFile)
                );
                return JoinWithinRoot(outputPath, decryptedName);
            }

            // Has a file extension — treat as an explicit file path (explicit user intent,
            // absolute paths allowed)
            return outputPath;
        }

        // Multi-file mode (directory or glob): output path is always a directory
        string outputFileName = GenerateDecryptedFileName(fileSystem.Path.GetFileName(inputFile));
        return JoinWithinRoot(outputPath, outputFileName);
    }

    /// <summary>
    /// Joins a computed file name onto the user-supplied output directory and verifies the
    /// canonicalized result still lives directly under that directory. Guards against
    /// hostile input file names (e.g. containing <c>..</c>) escaping the output root when
    /// the CLI is driven by automation.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The resolved path escapes the output directory.
    /// </exception>
    private string JoinWithinRoot(string outputDirectory, string fileName)
    {
        string joined = fileSystem.Path.Join(outputDirectory, fileName);
        string canonicalRoot = fileSystem.Path.TrimEndingDirectorySeparator(
            fileSystem.Path.GetFullPath(outputDirectory)
        );
        string canonicalJoined = fileSystem.Path.GetFullPath(joined);

        string? parent = fileSystem.Path.GetDirectoryName(canonicalJoined);
        // Both paths canonicalize from the same outputDirectory string, so a compliant
        // file name yields an exactly equal parent; ordinal comparison avoids
        // case-sensitivity surprises across filesystems.
        if (!string.Equals(parent, canonicalRoot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resolved output path '{canonicalJoined}' escapes the output directory '{canonicalRoot}'."
            );
        }

        return joined;
    }

    /// <summary>
    /// Generates a decrypted filename by inserting <c>.decrypted</c> before the extension.
    /// Example: <c>app.log</c> → <c>app.decrypted.log</c>
    /// </summary>
    private string GenerateDecryptedFileName(string fileName)
    {
        string nameWithoutExtension = fileSystem.Path.GetFileNameWithoutExtension(fileName);
        string extension = fileSystem.Path.GetExtension(fileName);
        return $"{nameWithoutExtension}.decrypted{extension}";
    }
}
