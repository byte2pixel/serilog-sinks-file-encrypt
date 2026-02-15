using System.Text;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Writers;

/// <summary>
/// Session writer for version 1 of the encrypted log format.
/// This implementation handles the encoding of session metadata into the header format using RSA encryption,
/// encrypts log messages using AES-GCM, and writes the framed session data to the output stream according to
/// the specified format for version 1.
/// </summary>
internal sealed class SessionWriterV1 : ISessionWriter
{
    private readonly IFrameWriter _frameWriter;
    private readonly IHeaderEncryptor _headerEncryptor;
    private readonly IMessageEncryptor _messageEncryptor;
    private readonly string _keyId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionWriterV1"/> class with the specified header encoder, message encryptor, and frame writer.
    /// </summary>
    /// <param name="headerEncryptor">The header encoder responsible for encoding session metadata into the header format.</param>
    /// <param name="messageEncryptor">The message encryptor responsible for encrypting the log messages using AES-GCM.</param>
    /// <param name="keyId">The keyId for the RSA key used to encrypt the data.</param>
    /// <param name="frameWriter">The frame writer responsible for writing the framed session data to the output stream according to the specified format.</param>
    internal SessionWriterV1(
        IHeaderEncryptor headerEncryptor,
        IMessageEncryptor messageEncryptor,
        string keyId = "",
        IFrameWriter? frameWriter = null
    )
    {
        _keyId = keyId;
        _frameWriter = frameWriter ?? new FrameWriter();
        _headerEncryptor = headerEncryptor;
        _messageEncryptor = messageEncryptor;
    }

    /// <inheritdoc />
    public void WriteSession(Stream output, SessionData session, ReadOnlySpan<byte> buffer)
    {
        // 1. Encrypt header using spans
        byte[] header = _headerEncryptor.Encrypt(session.AesKey, session.Nonce, session.Timestamp);

        // 2. Calculate encrypted message length
        int encryptedMessageLength = _messageEncryptor.GetEncryptedLength(buffer.Length);

        // 3. Compute session length
        int sessionLength = header.Length + encryptedMessageLength;

        // 4. Write the 32 byte keyId padded or throw if too long for the header format
        var keyIdBytes = Encoding.UTF8.GetBytes(_keyId).AsSpan();
        if (keyIdBytes.Length > 32)
        {
            throw new InvalidOperationException(
                $"KeyId is too long for the header format. Maximum length is 32 bytes, but was {keyIdBytes.Length} bytes."
            );
        }

        Span<byte> paddedKeyIdBytes = stackalloc byte[32];
        keyIdBytes.CopyTo(paddedKeyIdBytes);

        // 4. Write framing header
        _frameWriter.WriteHeader(output, 1, paddedKeyIdBytes.ToArray(), header, sessionLength);

        // 5. Encrypt and write message directly to stream (no intermediate allocation)
        _messageEncryptor.EncryptAndWrite(output, session, buffer);
    }
}
