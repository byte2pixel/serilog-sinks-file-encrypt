namespace Serilog.Sinks.File.Encrypt.Cli;

/// <summary>
/// Service interface for resolving file paths from user input. This allows the application to support various input formats, such as single files or directories with glob patterns, while abstracting away the underlying file system operations. Implementations of this interface can handle the logic for parsing the input path and returning a list of file paths that match the criteria specified by the user.
/// </summary>
public interface IInputResolver
{
    /// <summary>
    /// Resolves the input path to a list of file paths. The input path can be a single file or directory with a glob pattern.
    /// </summary>
    /// <param name="inputPath">The input path to resolve.</param>
    /// <returns></returns>
    IReadOnlyList<string> ResolveFiles(string inputPath);
}
