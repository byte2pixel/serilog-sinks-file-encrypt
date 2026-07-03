using System.Buffers.Binary;

namespace Serilog.Sinks.File.Tests.Shared;

/// <summary>
/// Hand-rolled writer for the legacy v1 on-disk format, independent of the product writers
/// (which emit v2 as of v6.0.0). Used to build v1 sessions in-memory for backward-compatibility
/// and mixed-version tests with the test run's own RSA key. The committed binary fixtures under
/// <c>Fixtures\v1</c> remain the authoritative pin of the shipped v1 bytes; this builder exists
/// for combinatorial cases the static fixtures cannot cover.
/// </summary>
public static class V1TestStreamBuilder
{
    /// <summary>
    /// Appends a complete v1 session (header + one frame per message, AES-GCM with
    /// <c>associatedData: null</c>) to <paramref name="output"/>.
    /// </summary>
    public static void WriteSession(
        Stream output,
        RSA publicRsa,
        string keyId,
        IEnumerable<string> messages
    )
    {
        byte[] aesKey = RandomNumberGenerator.GetBytes(32);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);

        // Header: magic(8) + version(1) + keyId(32, zero-padded UTF-8) + RSA-OAEP-SHA256(key ‖ nonce)
        output.Write(CryptographicUtils.MagicBytes);
        output.WriteByte(1);

        byte[] keyIdBytes = new byte[32];
        Encoding.UTF8.GetBytes(keyId, keyIdBytes);
        output.Write(keyIdBytes);

        byte[] rsaPayload = new byte[44];
        aesKey.CopyTo(rsaPayload, 0);
        nonce.CopyTo(rsaPayload, 32);
        output.Write(publicRsa.Encrypt(rsaPayload, RSAEncryptionPadding.OaepSHA256));

        // Frames: [4-byte BE length = ctLen + 16][ciphertext][16-byte tag], nonce counter
        // (last 8 bytes, little-endian) incremented per frame.
        using var aesGcm = new AesGcm(aesKey, 16);
        Span<byte> lengthPrefix = stackalloc byte[sizeof(int)];
        foreach (string message in messages)
        {
            byte[] plaintext = Encoding.UTF8.GetBytes(message);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[16];
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData: null);

            long counter = BinaryPrimitives.ReadInt64LittleEndian(nonce.AsSpan(4));
            BinaryPrimitives.WriteInt64LittleEndian(nonce.AsSpan(4), counter + 1);

            BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, ciphertext.Length + 16);
            output.Write(lengthPrefix);
            output.Write(ciphertext);
            output.Write(tag);
        }
    }
}
