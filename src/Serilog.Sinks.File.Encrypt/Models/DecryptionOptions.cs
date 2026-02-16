using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Decryption options for Serilog.Sinks.File.Encrypt decryption tools.
/// </summary>
public sealed record DecryptionOptions
{
    /// <summary>
    /// A dictionary of RSA private keys indexed by their corresponding key IDs. This allows for key rotation and supports multiple keys for decryption.
    /// </summary>
    public required Dictionary<string, string> DecryptionKeys { get; init; } = [];

    /// <summary>
    /// Whether to continue processing after encountering decryption errors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When true (default), corrupted sections are handled according to <see cref="ErrorHandlingMode"/>.
    /// When false, decryption stops immediately on first error.
    /// </para>
    /// <para>
    /// <b>Recommendation:</b> Set to true for production logs where partial recovery is acceptable.
    /// Set to false for audit logs where completeness is critical.
    /// </para>
    /// </remarks>
    public bool ContinueOnError { get; init; } = true;

    /// <summary>
    /// Defines how decryption errors should be handled when <see cref="ContinueOnError"/> is true.
    /// </summary>
    /// <remarks>
    /// See <see cref="Models.ErrorHandlingMode"/> for detailed descriptions of each mode.
    /// Default is <see cref="ErrorHandlingMode.Skip"/> which silently skips corrupted sections.
    /// </remarks>
    public ErrorHandlingMode ErrorHandlingMode { get; init; } = ErrorHandlingMode.Skip;

    /// <summary>
    /// Path to write error log file when <see cref="ErrorHandlingMode"/> is <see cref="Models.ErrorHandlingMode.WriteToErrorLog"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If null when using <see cref="ErrorHandlingMode.WriteToErrorLog"/>, a default path will be generated
    /// in the system temp directory with a timestamp.
    /// </para>
    /// <para>
    /// <b>File Format:</b> Plain text with timestamps, positions, and error messages.
    /// The directory will be created if it doesn't exist.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = new StreamingOptions
    /// {
    ///     ErrorHandlingMode = ErrorHandlingMode.WriteToErrorLog,
    ///     ErrorLogPath = Path.Join("logs", "decryption_errors.log")
    /// };
    /// </code>
    /// </example>
    public string? ErrorLogPath { get; init; }
};
