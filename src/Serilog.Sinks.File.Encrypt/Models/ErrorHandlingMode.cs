namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Defines how decryption errors should be handled during streaming decryption
/// </summary>
public enum ErrorHandlingMode
{
    /// <summary>
    /// Skip corrupted sections silently and continue decryption (default - safe for structured logs)
    /// </summary>
    Skip = 0,

    /// <summary>
    /// Write error messages inline to the output stream (use for human-readable logs only)
    /// </summary>
    WriteInline = 1,

    /// <summary>
    /// Write errors to a separate error log file
    /// </summary>
    WriteToErrorLog = 2,

    /// <summary>
    /// Throw an exception and stop decryption on first error
    /// </summary>
    ThrowException = 3,
}
