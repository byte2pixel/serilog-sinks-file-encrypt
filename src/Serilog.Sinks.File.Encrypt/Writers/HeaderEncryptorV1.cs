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
    private readonly string _keyId;

    /// <inheritdoc />
    public HeaderMetadata Metadata { get; } = HeaderMetadata.CreateV1();

    /// <summary>
    /// Initializes the header encoder with the RSA public key and optional KeyId.
    /// </summary>
    /// <param name="options">The encryption options containing the RSA public key and optional KeyId.</param>
    /// <exception cref="ArgumentException">Thrown when the KeyId is too long for the RSA key size.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the options or public key is null.</exception>
    internal HeaderEncryptorV1(EncryptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _publicKey = options.PublicKey;
        _keyId = options.KeyId ?? string.Empty;

        // Validate KeyId size during construction to fail fast
        ValidateKeyIdSize();
    }

    /// <summary>
    /// Validates that the KeyId length is compatible with the RSA key size and header format.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when KeyId exceeds recommended limits.</exception>
    private void ValidateKeyIdSize()
    {
        int maxKeyIdSize = Metadata.GetMaxVariableFieldSize(_publicKey.KeySize);
        int keyIdBytes = Encoding.UTF8.GetByteCount(_keyId);

        if (keyIdBytes > maxKeyIdSize)
        {
            throw new ArgumentException(
                $"KeyId length ({keyIdBytes} bytes) exceeds recommended maximum ({maxKeyIdSize} bytes) "
                    + $"for RSA-{_publicKey.KeySize} key with {Metadata.Padding.Mode} padding in header version {Metadata.Version}. "
                    + $"Header format: {Metadata.FieldDescription}. "
                    + $"Please use a shorter KeyId or a larger RSA key size.",
                nameof(EncryptionOptions.KeyId)
            );
        }
    }

    /// <inheritdoc />
    public byte[] Encrypt(SessionData session)
    {
        // Build the header payload (plaintext)
        byte[] payload = BuildHeaderPayload(session);

        // Validate payload size before encryption
        RsaEncryptionHelper.ValidatePayloadSize(
            payload.Length,
            _publicKey.KeySize,
            Metadata.Padding
        );

        // RSA encrypt the payload
        return _publicKey.Encrypt(payload, Metadata.Padding);
    }

    private byte[] BuildHeaderPayload(SessionData session)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // KeyId length + KeyId (optional)
        byte[] keyIdBytes = Encoding.UTF8.GetBytes(_keyId); // FIX: encode full KeyId
        bw.Write((byte)keyIdBytes.Length);
        bw.Write(keyIdBytes);

        // AES key
        bw.Write((byte)session.AesKey.Length);
        bw.Write(session.AesKey);

        // Nonce
        bw.Write((byte)session.Nonce.Length);
        bw.Write(session.Nonce);

        // Optional metadata (timestamp, sequence)
        bw.Write(session.Timestamp.ToUnixTimeMilliseconds());

        return ms.ToArray();
    }
}
