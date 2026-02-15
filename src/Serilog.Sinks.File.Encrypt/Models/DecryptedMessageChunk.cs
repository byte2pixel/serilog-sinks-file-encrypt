namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Represents a decrypted message chunk containing the plaintext data.
/// </summary>
public record DecryptedMessageChunk : IDecryptionChunk
{
    /// <summary>
    /// The decrypted message content as a string.
    /// </summary>
    public string Content { get; init; }

    /// <summary>
    /// The decrypted message data as bytes.
    /// </summary>
    public byte[] Data { get; init; }

    /// <summary>
    /// Creates a DecryptedMessageChunk from raw byte data.
    /// </summary>
    public DecryptedMessageChunk(byte[] data)
    {
        Data = data;
        Content = System.Text.Encoding.UTF8.GetString(data);
    }

    /// <summary>
    /// Creates a DecryptedMessageChunk from a string.
    /// </summary>
    public DecryptedMessageChunk(string content)
    {
        Content = content;
        Data = System.Text.Encoding.UTF8.GetBytes(content);
    }
}
