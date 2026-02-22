namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// The result of a decryption operation, containing counts of decrypted sessions, messages, failures, and resync
/// attempts.
/// </summary>
public class DecryptionResult
{
    /// <summary>
    /// Number of successfully decrypted sessions.
    /// A session is defined as a complete set of log messages that were encrypted together.
    /// </summary>
    public int DecryptedSessions { get; init; } = 0;

    /// <summary>
    /// Number of successfully decrypted messages.
    /// This counts individual log messages that were successfully decrypted, regardless of session boundaries.
    /// </summary>
    public int DecryptedMessages { get; init; } = 0;

    /// <summary>
    /// The number of decryption errors encountered while processing what appears
    /// to be a session header. This indicates issues with the session metadata,
    /// such as missing or corrupted headers that prevent identifying the session's
    /// decryption parameters.
    /// </summary>
    public int FailedHeaders { get; init; } = 0;

    /// <summary>
    /// The number of decryption errors encountered while processing individual log messages.
    /// This indicates issues with the message content, such as corruption or decryption failures
    /// that occur after successfully identifying the session. These errors may be due to data corruption,
    /// incorrect decryption keys, or other issues that prevent successful decryption of the message content
    /// </summary>
    public int FailedMessages { get; init; } = 0;

    /// <summary>
    /// <para>
    /// The number of times the decryption process had to attempt resynchronization after encountering errors.
    /// Resynchronization attempts to occur when the decryption process detects a corrupted section and tries to skip
    /// past it to continue processing subsequent messages.
    /// </para>
    /// <para>
    /// A high number of resynchronization attempts may indicate significant corruption in the log file,
    /// while a low number suggests that most of the file was successfully decrypted with minimal issues.
    /// </para>
    /// </summary>
    public int ResyncAttempts { get; init; } = 0;
}
