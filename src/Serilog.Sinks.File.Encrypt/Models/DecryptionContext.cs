namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Represents the current decryption context with active encryption keys
/// </summary>
public class DecryptionContext(byte[] nonce, byte[] sessionKey)
{
    public byte[] SessionKey { get; } = sessionKey;

    public byte[] Nonce { get; } = nonce;

    public static DecryptionContext Empty => new([], []);

    public bool HasKeys => Nonce.Length > 0 && SessionKey.Length > 0;
}
