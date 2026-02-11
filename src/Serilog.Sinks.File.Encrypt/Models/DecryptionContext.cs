namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Represents the current decryption context with active encryption keys
/// </summary>
internal class DecryptionContext(int tagLength, byte[] nonce, byte[] sessionKey)
{
    public int TagLength { get; } = tagLength;

    public byte[] SessionKey { get; } = sessionKey;

    public byte[] Nonce { get; } = nonce;

    public static DecryptionContext Empty => new(0, [], []);

    public bool HasKeys => Nonce.Length > 0 && SessionKey.Length > 0 && TagLength > 0;
}
