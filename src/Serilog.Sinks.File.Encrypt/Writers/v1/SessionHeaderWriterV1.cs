using System.Text;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Writers.v1;

/// <summary>
/// Session header writer for version 1 of the encrypted log format.
/// Writes the session header (magic bytes, version, keyId, RSA-encrypted session info) once per session.
/// </summary>
internal sealed class SessionHeaderWriterV1 : ISessionHeaderWriter
{
    private readonly IFrameWriter _frameWriter;
    private readonly IHeaderEncryptor _headerEncryptor;
    private readonly string _keyId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionHeaderWriterV1"/> class.
    /// </summary>
    /// <param name="headerEncryptor">The header encryptor responsible for RSA-encrypting the session metadata.</param>
    /// <param name="keyId">The key ID for the RSA key used to encrypt the session data.</param>
    /// <param name="frameWriter">Optional frame writer for writing the framed header. Defaults to <see cref="FrameWriter"/>.</param>
    internal SessionHeaderWriterV1(
        IHeaderEncryptor headerEncryptor,
        string keyId = "",
        IFrameWriter? frameWriter = null
    )
    {
        _keyId = keyId;
        _frameWriter = frameWriter ?? new FrameWriter();
        _headerEncryptor = headerEncryptor;
    }

    /// <inheritdoc />
    public void WriteHeader(Stream output, SessionData session)
    {
        // 1. Encrypt header using RSA (contains AES key, nonce, timestamp)
        byte[] header = _headerEncryptor.Encrypt(session.AesKey, session.Nonce, session.Timestamp);

        // 2. Write the 32 byte keyId padded or throw if too long for the header format
        var keyIdBytes = Encoding.UTF8.GetBytes(_keyId).AsSpan();
        if (keyIdBytes.Length > 32)
        {
            throw new InvalidOperationException(
                $"KeyId is too long for the header format. Maximum length is 32 bytes, but was {keyIdBytes.Length} bytes."
            );
        }

        Span<byte> paddedKeyIdBytes = stackalloc byte[32];
        keyIdBytes.CopyTo(paddedKeyIdBytes);

        // 3. Write framing header (magic bytes + version + keyId + RSA payload)
        _frameWriter.WriteHeader(output, 1, paddedKeyIdBytes.ToArray(), header);
    }
}
