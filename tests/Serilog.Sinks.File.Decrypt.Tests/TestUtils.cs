namespace Serilog.Sinks.File.Decrypt.Tests;

internal static class TestUtils
{
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
    internal static byte[] CorruptDataAddingMarker(byte[] data, byte[] marker, int position)
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
    internal static (byte[] aesKey, byte[] nonce) CreateSessionData()
    {
        byte[] key = RandomNumberGenerator.GetBytes(EncryptionConstants.SessionKeyLength);
        byte[] nonce = RandomNumberGenerator.GetBytes(EncryptionConstants.NonceLength);
        return (key, nonce);
    }
}
