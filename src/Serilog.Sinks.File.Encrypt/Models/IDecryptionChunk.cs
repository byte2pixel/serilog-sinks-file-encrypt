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
/// Contains the necessary information to decrypt a log message, including the AES session key, nonce, timestamp, and the encrypted payload.
/// </summary>
/// <param name="Version">The version of this session.</param>
/// <param name="AesKey">The aes key for this session.</param>
/// <param name="Nonce">The nonce for this session.</param>
/// <param name="Timestamp">The timestamp this session was written.</param>
/// <param name="Payload">The encrypted payload of this session.</param>
public record DecryptionSessionChunk(
    byte Version,
    byte[] AesKey,
    byte[] Nonce,
    DateTimeOffset Timestamp,
    ReadOnlyMemory<byte> Payload
) : IDecryptionChunk;

/// <summary>
/// Indicates the end of the decryption stream
/// </summary>
internal record EndOfStreamChunk : IDecryptionChunk
{
    public static readonly EndOfStreamChunk Instance = new();
}
