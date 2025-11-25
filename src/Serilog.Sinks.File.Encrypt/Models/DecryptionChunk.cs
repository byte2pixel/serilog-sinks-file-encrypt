namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Represents a chunk of data to be processed by the consumer
/// </summary>
internal abstract record DecryptionChunk;

/// <summary>
/// A successfully decrypted message chunk
/// </summary>
internal record DecryptedMessageChunk(string Content) : DecryptionChunk;

/// <summary>
/// An error that occurred during decryption
/// </summary>
internal record DecryptionErrorChunk(string ErrorMessage, long Position) : DecryptionChunk;

/// <summary>
/// Indicates the end of the decryption stream
/// </summary>
internal record EndOfStreamChunk : DecryptionChunk
{
    public static readonly EndOfStreamChunk Instance = new();
}
