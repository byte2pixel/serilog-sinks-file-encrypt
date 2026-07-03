using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Session header writer for the encrypted log format.
/// Writes the session header (magic bytes, version, keyId, RSA-encrypted session info) once per session
/// and produces the SHA-256 hash of the written header bytes for associated-data binding.
/// </summary>
internal sealed class SessionWriter : ISessionWriter
{
    private readonly IFrameWriter _frameWriter;
    private readonly IHeaderWriter _headerWriter;
    private readonly string _keyId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionWriter"/> class.
    /// </summary>
    /// <param name="headerWriter">The header encryptor responsible for RSA-encrypting the session metadata.</param>
    /// <param name="keyId">The key ID for the RSA key used to encrypt the session data.</param>
    /// <param name="frameWriter">Optional frame writer for composing the framed header. Defaults to <see cref="FrameWriter"/>.</param>
    internal SessionWriter(
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
    public void WriteHeader(
        Stream output,
        ReadOnlySpan<byte> aesKey,
        ReadOnlySpan<byte> nonce,
        Span<byte> headerHash
    )
    {
        Span<byte> keyIdBytes = Encoding.UTF8.GetBytes(_keyId);
        if (keyIdBytes.Length > HeaderMetadata.KeyIdLength)
        {
            throw new InvalidOperationException(
                $"KeyId is too long for the header format. Maximum length is 32 bytes, but was {keyIdBytes.Length} bytes."
            );
        }
        // 1. Encrypt header using RSA
        ReadOnlySpan<byte> header = _headerWriter.Encrypt(aesKey, nonce);

        // 2. Write the 32 byte keyId padded or throw if too long for the header format
        Span<byte> paddedKeyIdBytes = stackalloc byte[HeaderMetadata.KeyIdLength];
        keyIdBytes.CopyTo(paddedKeyIdBytes);

        // 3. Compose the framing header (magic bytes + version + keyId + RSA payload) into one
        //    contiguous buffer so it can be written and hashed as the exact same bytes.
        int headerLength =
            CryptographicUtils.MagicBytes.Length + 1 + HeaderMetadata.KeyIdLength + header.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(headerLength);
        try
        {
            int written = _frameWriter.WriteHeader(
                buffer.AsSpan(0, headerLength),
                EncryptionConstants.CurrentFormatVersion,
                paddedKeyIdBytes,
                header
            );
            output.Write(buffer, 0, written);
            SHA256.HashData(buffer.AsSpan(0, written), headerHash);
        }
        finally
        {
            Array.Clear(buffer, 0, headerLength);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
