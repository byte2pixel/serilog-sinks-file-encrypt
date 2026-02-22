namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Defines how decryption errors should be handled during streaming decryption operations.
/// </summary>
public enum ErrorHandlingMode
{
    /// <summary>
    /// Skip corrupted sections silently and continue decryption (default).
    /// </summary>
    Skip = 0,

    /// <summary>
    /// Throw an exception and stop decryption immediately on the first error.
    /// </summary>
    ThrowException = 1,
}
