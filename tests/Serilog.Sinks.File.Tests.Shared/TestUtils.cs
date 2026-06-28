namespace Serilog.Sinks.File.Tests.Shared;

public static class TestUtils
{
    public static EncryptionOptions GetEncryptionOptions(
        RSA publicRsa,
        string? keyId = null,
        int version = 1
    )
    {
        return new EncryptionOptions(publicRsa, keyId ?? "", version);
    }

    public static EncryptionOptions GetEncryptionOptions(
        string publicKey,
        string? keyId = null,
        int version = 1
    )
    {
        var rsa = RSA.Create();
        rsa.FromString(publicKey);
        return GetEncryptionOptions(rsa, keyId, version);
    }

    /// <summary>
    /// Creates a corrupted version of encrypted data by flipping bits at the specified position.
    /// </summary>
    public static byte[] CorruptData(byte[] data, int position)
    {
        byte[] corrupted = new byte[data.Length];
        Array.Copy(data, corrupted, data.Length);
        corrupted[position] ^= 0xFF;
        return corrupted;
    }

    /// <summary>
    /// Corrupts data by inserting specific marker bytes at the given position.
    /// </summary>
    public static byte[] CorruptDataAddingMarker(byte[] data, byte[] marker, int position)
    {
        byte[] corrupted = new byte[data.Length];
        Array.Copy(data, corrupted, data.Length);
        for (int i = 0; i < marker.Length && (position + i) < corrupted.Length; i++)
        {
            corrupted[position + i] = marker[i];
        }
        return corrupted;
    }

    /// <summary>
    /// Creates a new session data with random AES key and nonce.
    /// </summary>
    public static (byte[] aesKey, byte[] nonce) CreateSessionData()
    {
        byte[] key = RandomNumberGenerator.GetBytes(EncryptionConstants.SessionKeyLength);
        byte[] nonce = RandomNumberGenerator.GetBytes(EncryptionConstants.NonceLength);
        return (key, nonce);
    }
}