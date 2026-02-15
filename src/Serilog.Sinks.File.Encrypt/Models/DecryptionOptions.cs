using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Decryption options for Serilog.Sinks.File.Encrypt decryption tools.
/// </summary>
public record DecryptionOptions
{
    /// <summary>
    /// A dictionary of RSA private keys indexed by their corresponding key IDs. This allows for key rotation and supports multiple keys for decryption.
    /// </summary>
    public required Dictionary<string, RSA> DecryptionKeys { get; init; } = [];

    /// <summary>
    /// Default buffer size for decryption chunks (16KB)
    /// </summary>
    private const int DefaultBufferSize = 16 * 1024;

    /// <summary>
    /// Default queue depth for producer-consumer pattern
    /// </summary>
    private const int DefaultQueueDepth = 10;

    /// <summary>
    /// Buffer size for processing encryption chunks in bytes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Larger buffers improve throughput for large files but increase memory usage.
    /// Recommended range: 8KB - 64KB.
    /// </para>
    /// <para>
    /// <b>Performance Impact:</b>
    /// - 8KB: Low memory, suitable for memory-constrained environments
    /// - 16KB: Balanced (default)
    /// - 32KB-64KB: High throughput for large files
    /// </para>
    /// </remarks>
    public int BufferSize { get; init; } = DefaultBufferSize;

    /// <summary>
    /// Maximum number of chunks to queue between producer and consumer threads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Higher values allow more parallelism but increase memory usage.
    /// Total buffered memory ≈ BufferSize × QueueDepth.
    /// Recommended range: 5 - 50.
    /// </para>
    /// <para>
    /// <b>Performance Impact:</b>
    /// - 5-10: Balanced (default: 10)
    /// - 20-50: High throughput scenarios
    /// - 1-5: Memory-constrained environments
    /// </para>
    /// </remarks>
    public int QueueDepth { get; init; } = DefaultQueueDepth;

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
