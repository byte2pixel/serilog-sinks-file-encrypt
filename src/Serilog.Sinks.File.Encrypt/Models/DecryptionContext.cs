namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Represents the current decryption context with active encryption keys
/// </summary>
/// <param name="nonce"></param>
/// <param name="sessionKey"></param>
internal class DecryptionContext(byte[] nonce, byte[] sessionKey)
{
    /// <summary>
    /// The AES-GCM Session Key.
    /// </summary>
    public byte[] SessionKey { get; } = sessionKey;

    /// <summary>
    /// The AES-GCM Nonce (Initialization Vector) used for decryption.
    /// </summary>
    public byte[] Nonce { get; } = nonce;

    /// <summary>
    /// Creates an empty decryption content.
    /// </summary>
    public static DecryptionContext Empty => new([], []);

    /// <summary>
    /// Returns true if both the Nonce and SessionKey are present, indicating that decryption can proceed.
    /// </summary>
    public bool HasKeys => Nonce.Length > 0 && SessionKey.Length > 0;
}
