using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Defines how decryption errors should be handled during streaming decryption operations.
/// </summary>
/// <remarks>
/// <para>
/// Choose an error handling mode based on your use case:
/// - Structured logs (JSON, etc.): Use <see cref="Skip"/> to preserve parseable output
/// - Human-readable logs: Use <see cref="WriteInline"/> for visibility
/// - Audit/compliance: Use <see cref="WriteToErrorLog"/> or <see cref="ThrowException"/>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Skip corrupted sections silently (best for structured logs)
/// var skipOptions = new StreamingOptions
/// {
///     ContinueOnError = true,
///     ErrorHandlingMode = ErrorHandlingMode.Skip
/// };
///
/// // Write errors inline (good for debugging)
/// var inlineOptions = new StreamingOptions
/// {
///     ContinueOnError = true,
///     ErrorHandlingMode = ErrorHandlingMode.WriteInline
/// };
///
/// // Log errors separately (audit trail)
/// var auditOptions = new StreamingOptions
/// {
///     ContinueOnError = true,
///     ErrorHandlingMode = ErrorHandlingMode.WriteToErrorLog,
///     ErrorLogPath = "audit/decryption_errors.log"
/// };
///
/// // Fail fast (strict validation)
/// var strictOptions = new StreamingOptions
/// {
///     ContinueOnError = false,
///     ErrorHandlingMode = ErrorHandlingMode.ThrowException
/// };
/// </code>
/// </example>
public enum ErrorHandlingMode
{
    /// <summary>
    /// Skip corrupted sections silently and continue decryption (default - safest for structured logs).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Corrupted data is omitted from the output without any indication. This preserves the structure
    /// of parseable log formats (JSON, XML, etc.) and prevents corruption markers from breaking parsers.
    /// </para>
    /// <para>
    /// <b>Use When:</b> Logs are structured/machine-readable and partial recovery is acceptable.
    /// </para>
    /// <para>
    /// <b>Output:</b> Clean, parseable log entries without corruption markers.
    /// </para>
    /// </remarks>
    Skip = 0,

    /// <summary>
    /// Write human-readable error messages inline to the output stream (use for human-readable logs only).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inserts error messages like "[DECRYPTION ERROR at position 12345: ...]" directly into the output.
    /// This makes issues visible but may break structured log parsers.
    /// </para>
    /// <para>
    /// <b>Use When:</b> Logs are plain text and intended for human reading, debugging is needed.
    /// </para>
    /// <para>
    /// <b>Warning:</b> Do NOT use with structured logs (JSON, XML) as error markers will corrupt the format.
    /// </para>
    /// </remarks>
    WriteInline = 1,

    /// <summary>
    /// Write error details to a separate error log file for audit purposes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Creates/appends to a separate error log file with timestamps, file positions, and error details.
    /// The main decrypted output remains clean and parseable.
    /// </para>
    /// <para>
    /// <b>Use When:</b> You need an audit trail of decryption issues without corrupting the main output.
    /// </para>
    /// <para>
    /// <b>Output:</b> Clean decrypted log + separate error log with detailed diagnostics.
    /// </para>
    /// </remarks>
    WriteToErrorLog = 2,

    /// <summary>
    /// Throw an exception and stop decryption immediately on the first error.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Decryption stops at the first error with a <see cref="CryptographicException"/>.
    /// No partial output is produced. Use when data integrity is critical.
    /// </para>
    /// <para>
    /// <b>Use When:</b> Completeness and integrity are critical (audit logs, compliance, etc.).
    /// </para>
    /// <para>
    /// <b>Note:</b> Requires <see cref="StreamingOptions.ContinueOnError"/> to be false.
    /// </para>
    /// </remarks>
    ThrowException = 3,
}
