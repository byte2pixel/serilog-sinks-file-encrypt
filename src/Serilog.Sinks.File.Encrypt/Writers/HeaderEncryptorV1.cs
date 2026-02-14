using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Writers;

/// <summary>
/// Version 1 header encoder that constructs a header containing the AES session key, nonce, and optional KeyId,
/// </summary>
internal sealed class HeaderEncryptorV1 : IHeaderEncryptor
{
    private readonly RSA _publicKey;
    private readonly byte[] _keyIdBytes;

    public HeaderMetadata Metadata { get; } = HeaderMetadata.CreateV1();

    /// <summary>
    /// Initializes the header encoder with the RSA public key and optional KeyId.
    /// </summary>
    /// <param name="options">The encryption options containing the RSA public key and optional KeyId.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the KeyId is too long for the RSA key size.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the options or public key is null.</exception>
    internal HeaderEncryptorV1(EncryptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _publicKey = options.PublicKey;
        string keyId = options.KeyId ?? string.Empty;
        _keyIdBytes = Encoding.UTF8.GetBytes(keyId);

        int max = Metadata.GetMaxVariableFieldSize(_publicKey.KeySize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(_keyIdBytes.Length, max);
    }

    /// <inheritdoc />
    public byte[] Encrypt(
        ReadOnlySpan<byte> aesKey,
        ReadOnlySpan<byte> nonce,
        DateTimeOffset timestamp
    )
    {
        // Calculate exact payload size
        int payloadSize =
            1
            + _keyIdBytes.Length
            + // KeyId length + KeyId
            1
            + aesKey.Length
            + // AES key length + key
            1
            + nonce.Length
            + // Nonce length + nonce
            8; // Timestamp (long)

        // Rent buffer from pool
        byte[] payload = ArrayPool<byte>.Shared.Rent(payloadSize);
        try
        {
            int offset = 0;

            // Write KeyId length + KeyId
            payload[offset++] = (byte)_keyIdBytes.Length;
            _keyIdBytes.CopyTo(payload.AsSpan(offset));
            offset += _keyIdBytes.Length;

            // Write AES key length + key
            payload[offset++] = (byte)aesKey.Length;
            aesKey.CopyTo(payload.AsSpan(offset));
            offset += aesKey.Length;

            // Write Nonce length + nonce
            payload[offset++] = (byte)nonce.Length;
            nonce.CopyTo(payload.AsSpan(offset));
            offset += nonce.Length;

            // Write timestamp
            long timestampMs = timestamp.ToUnixTimeMilliseconds();
            BitConverter.TryWriteBytes(payload.AsSpan(offset), timestampMs);

            // Validate payload size before encryption
            RsaEncryptionHelper.ValidatePayloadSize(
                payloadSize,
                _publicKey.KeySize,
                Metadata.Padding
            );

            // RSA encrypt the payload
            byte[] encrypted = _publicKey.Encrypt(
                payload.AsSpan(0, payloadSize).ToArray(),
                Metadata.Padding
            );

            return encrypted;
        }
        finally
        {
            // Clear sensitive data and return to pool
            CryptographicOperations.ZeroMemory(payload.AsSpan(0, payloadSize));
            ArrayPool<byte>.Shared.Return(payload);
        }
    }
}
