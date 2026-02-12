using System.Security.Cryptography;
using System.Text;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Writers;

/// <summary>
/// Version 1 header encoder that constructs a header containing the AES session key, nonce, and optional KeyId,
/// </summary>
public sealed class HeaderEncoderV1 : IHeaderEncoder
{
    private readonly RSA _publicKey;
    private readonly string _keyId;

    /// <summary>
    /// Initializes the header encoder with the RSA public key and optional KeyId.
    /// </summary>
    /// <param name="options">The encryption options containing the RSA public key and optional KeyId.</param>
    public HeaderEncoderV1(EncryptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _publicKey = options.PublicKey;
        _keyId = options.KeyId ?? string.Empty;
    }

    /// <inheritdoc />
    public byte[] EncodeHeader(SessionData session)
    {
        // Build the header payload (plaintext)
        byte[] payload = BuildHeaderPayload(session);

        // RSA encrypt the payload
        return _publicKey.Encrypt(payload, RSAEncryptionPadding.OaepSHA256);
    }

    private byte[] BuildHeaderPayload(SessionData session)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // KeyId length + KeyId (optional)
        byte[] keyIdBytes = Encoding.UTF8.GetBytes(_keyId);
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
