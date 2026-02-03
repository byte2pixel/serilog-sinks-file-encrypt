namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Represents the current decryption context with active encryption keys
/// </summary>
internal class DecryptionContext
{
    public DecryptionContext(int tagLength, byte[] nonce, byte[] sessionKey)
    {
        Nonce = nonce;
        SessionKey = sessionKey;
        TagLength = tagLength;
    }

    public int TagLength { get; init; }

    public byte[] SessionKey { get; init; }

    public byte[] Nonce { get; private set; }

    public static DecryptionContext Empty => new(0, [], []);

    public bool HasKeys => Nonce.Length > 0 && SessionKey.Length > 0 && TagLength > 0;
}
