namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Configuration options for streaming decryption
/// </summary>
public sealed class StreamingOptions
{
    /// <summary>
    /// Default buffer size for decryption chunks (16KB)
    /// </summary>
    private const int DefaultBufferSize = 16 * 1024;

    /// <summary>
    /// Default queue depth for producer-consumer pattern
    /// </summary>
    private const int DefaultQueueDepth = 10;

    /// <summary>
    /// Buffer size for processing chunks in bytes
    /// </summary>
    public int BufferSize { get; init; } = DefaultBufferSize;

    /// <summary>
    /// Maximum number of chunks to queue between producer and consumer
    /// </summary>
    public int QueueDepth { get; init; } = DefaultQueueDepth;

    /// <summary>
    /// Whether to continue processing after encountering decryption errors
    /// </summary>
    public bool ContinueOnError { get; init; } = true;

    /// <summary>
    /// Creates default streaming options
    /// </summary>
    public static StreamingOptions Default => new();
}
