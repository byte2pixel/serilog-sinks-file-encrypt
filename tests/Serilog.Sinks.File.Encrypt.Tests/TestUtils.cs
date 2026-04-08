namespace Serilog.Sinks.File.Encrypt.Tests;

internal static class TestUtils
{
    internal static EncryptionOptions GetEncryptionOptions(
        RSA publicRsa,
        string? keyId = null,
        int version = 1
    )
    {
        return new EncryptionOptions(publicRsa, keyId ?? "", version);
    }

    internal static EncryptionOptions GetEncryptionOptions(
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
    /// Creates a corrupted version of encrypted data by flipping bits at the specified position
    /// </summary>
    internal static byte[] CorruptData(byte[] data, int position)
    {
        byte[] corrupted = new byte[data.Length];
        Array.Copy(data, corrupted, data.Length);
        corrupted[position] ^= 0xFF; // Flip all bits at position
        return corrupted;
    }

    /// <summary>
    /// Corrupts data by inserting specific marker bytes at the given position
    /// </summary>
    /// <param name="data">The data to corrupt</param>
    /// <param name="position">The position to insert the marker</param>
    /// <param name="marker">The marker to insert</param>
    /// <returns>
    /// The corrupted data with the marker inserted at the specified position
    /// </returns>
    internal static byte[] CorruptDataAddingMarker(byte[] data, byte[] marker, int position)
    {
        byte[] corrupted = new byte[data.Length];
        Array.Copy(data, corrupted, data.Length);
        // Insert marker at position
        for (int i = 0; i < marker.Length && (position + i) < corrupted.Length; i++)
        {
            corrupted[position + i] = marker[i];
        }
        return corrupted;
    }

    /// <summary>
    /// Creates a new session data with random AES key and nonce.
    /// </summary>
    internal static (byte[] aesKey, byte[] nonce) CreateSessionData()
    {
        byte[] key = RandomNumberGenerator.GetBytes(EncryptionConstants.SessionKeyLength);
        byte[] nonce = RandomNumberGenerator.GetBytes(EncryptionConstants.NonceLength);
        return (key, nonce);
    }
}
