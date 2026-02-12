namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Represents an encrypted log message, containing the AES-GCM ciphertext and the associated authentication tag.
/// </summary>
public sealed class EncryptedMessage
{
    /// <summary>
    /// The AES-GCM encrypted bytes (ciphertext only)
    /// </summary>
    public byte[] Ciphertext { get; init; } = [];

    /// <summary>
    /// The 16-byte GCM authentication tag
    /// </summary>
    public byte[] Tag { get; init; } = [];

    /// <summary>
    /// Convenience: total bytes this message will occupy in the file
    /// </summary>
    public int TotalLength => Ciphertext.Length + Tag.Length;
}
