using System.Text;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Session header writer for version 1 of the encrypted log format.
/// Writes the session header (magic bytes, version, keyId, RSA-encrypted session info) once per session.
/// </summary>
internal sealed class SessionWriterV1 : ISessionWriter
{
    private readonly IFrameWriter _frameWriter;
    private readonly IHeaderWriter _headerWriter;
    private readonly string _keyId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionWriterV1"/> class.
    /// </summary>
    /// <param name="headerWriter">The header encryptor responsible for RSA-encrypting the session metadata.</param>
    /// <param name="keyId">The key ID for the RSA key used to encrypt the session data.</param>
    /// <param name="frameWriter">Optional frame writer for writing the framed header. Defaults to <see cref="FrameWriter"/>.</param>
    internal SessionWriterV1(
        IHeaderWriter headerWriter,
        string keyId = "",
        IFrameWriter? frameWriter = null
    )
    {
        _keyId = keyId;
        _frameWriter = frameWriter ?? new FrameWriter();
        _headerWriter = headerWriter;
    }

    /// <inheritdoc />
    public void WriteHeader(Stream output, ReadOnlySpan<byte> aesKey, ReadOnlySpan<byte> nonce)
    {
        Span<byte> keyIdBytes = Encoding.UTF8.GetBytes(_keyId);
        if (keyIdBytes.Length > HeaderMetadataV1.KeyIdLength)
        {
            throw new InvalidOperationException(
                $"KeyId is too long for the header format. Maximum length is 32 bytes, but was {keyIdBytes.Length} bytes."
            );
        }
        // 1. Encrypt header using RSA
        ReadOnlySpan<byte> header = _headerWriter.Encrypt(aesKey, nonce);

        // 2. Write the 32 byte keyId padded or throw if too long for the header format
        Span<byte> paddedKeyIdBytes = stackalloc byte[HeaderMetadataV1.KeyIdLength];
        keyIdBytes.CopyTo(paddedKeyIdBytes);

        // 3. Write framing header (magic bytes + version + keyId + RSA payload)
        _frameWriter.WriteHeader(output, 1, paddedKeyIdBytes, header);
    }
}
