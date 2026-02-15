using System.Buffers;
using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Writers;

/// <inheritdoc/>
internal class MessageEncryptorV1 : IMessageEncryptor
{
    /// <inheritdoc />
    public int GetEncryptedLength(int plaintextLength)
    {
        return plaintextLength + EncryptionConstants.TagLength;
    }

    /// <inheritdoc />
    public void EncryptAndWrite(Stream output, SessionData session, ReadOnlySpan<byte> buffer)
    {
        int plaintextLength = buffer.Length;

        // Rent buffers from pool
        byte[] ciphertext = ArrayPool<byte>.Shared.Rent(plaintextLength);
        byte[] tag = ArrayPool<byte>.Shared.Rent(EncryptionConstants.TagLength);

        try
        {
            // Encrypt directly into pooled buffers
            session.AesGcm.Encrypt(
                session.Nonce,
                buffer,
                ciphertext.AsSpan(0, plaintextLength),
                tag.AsSpan(0, EncryptionConstants.TagLength),
                associatedData: null
            );

            session.Nonce.IncreaseNonce();

            // Write encrypted data directly to stream from pooled buffers
            output.Write(ciphertext, 0, plaintextLength);
            output.Write(tag, 0, EncryptionConstants.TagLength);
        }
        finally
        {
            // Clear and return buffers to pool
            Array.Clear(ciphertext, 0, plaintextLength);
            ArrayPool<byte>.Shared.Return(ciphertext);
            ArrayPool<byte>.Shared.Return(tag);
        }
    }
}
