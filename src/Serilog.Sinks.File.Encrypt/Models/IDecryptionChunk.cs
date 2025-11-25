namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Represents a chunk of data to be processed by the consumer
/// </summary>
internal interface IDecryptionChunk;

/// <summary>
/// A successfully decrypted message chunk
/// </summary>
internal record DecryptedMessageChunk(string Content) : IDecryptionChunk;

/// <summary>
/// An error that occurred during decryption
/// </summary>
internal record DecryptionErrorChunk(string ErrorMessage, long Position) : IDecryptionChunk;

/// <summary>
/// Indicates the end of the decryption stream
/// </summary>
internal record EndOfStreamChunk : IDecryptionChunk
{
    public static readonly EndOfStreamChunk Instance = new();
}
