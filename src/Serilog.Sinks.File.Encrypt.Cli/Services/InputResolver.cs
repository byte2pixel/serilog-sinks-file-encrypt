using System.IO.Abstractions;

namespace Serilog.Sinks.File.Encrypt.Cli;

/// <inheritdoc />
public class InputResolver(IFileSystem fileSystem) : IInputResolver
{
    /// <inheritdoc />
    public IReadOnlyList<string> ResolveFiles(string inputPath, bool recursive)
    {
        List<string> files = [];
        string[] foundFiles;
        SearchOption searchOption;

        // Check if it's a direct file path
        if (fileSystem.File.Exists(inputPath))
        {
            files.Add(inputPath);
            return files;
        }

        // Check if it's a directory (always uses *.log pattern)
        if (fileSystem.Directory.Exists(inputPath))
        {
            searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foundFiles = fileSystem.Directory.GetFiles(inputPath, "*.log", searchOption);
            files.AddRange(FilterDecryptedFiles(foundFiles));
            return files;
        }

        // Check if it's a glob pattern (e.g., *.log, logs/*.txt)
        if (!inputPath.Contains('*') && !inputPath.Contains('?'))
        {
            return files;
        }

        string? directory = fileSystem.Path.GetDirectoryName(inputPath);
        string pattern = fileSystem.Path.GetFileName(inputPath);

        if (string.IsNullOrEmpty(directory))
        {
            directory = fileSystem.Directory.GetCurrentDirectory();
        }

        if (!fileSystem.Directory.Exists(directory) || string.IsNullOrEmpty(pattern))
        {
            return files;
        }

        searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        foundFiles = fileSystem.Directory.GetFiles(directory, pattern, searchOption);
        files.AddRange(FilterDecryptedFiles(foundFiles));

        return files;
    }

    /// <summary>
    /// Filters out files that have already been decrypted (contain .decrypted in the filename).
    /// This prevents attempting to re-decrypt already decrypted files.
    /// </summary>
    /// <param name="files">The list of files to filter.</param>
    /// <returns>Filtered list excluding already-decrypted files.</returns>
    private IEnumerable<string> FilterDecryptedFiles(string[] files)
    {
        return files.Where(f =>
        {
            string fileName = fileSystem.Path.GetFileName(f);
            return !fileName.Contains(".decrypted.", StringComparison.OrdinalIgnoreCase);
        });
    }
}
