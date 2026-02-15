using System.Buffers;
using System.Buffers.Binary;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Writers.v1;

/// <inheritdoc/>
internal class MessageEncryptorV1 : IMessageEncryptor
{
    /// <inheritdoc />
    public void EncryptAndWrite(Stream output, SessionData session, ReadOnlySpan<byte> buffer)
    {
        int plaintextLength = buffer.Length;
        int encryptedPayloadLength = plaintextLength + EncryptionConstants.TagLength;

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

            // Write 4-byte length prefix (big-endian) for self-framing
            Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(lengthBytes, encryptedPayloadLength);
            output.Write(lengthBytes);

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
