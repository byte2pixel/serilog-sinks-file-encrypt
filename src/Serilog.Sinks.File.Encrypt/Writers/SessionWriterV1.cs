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

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionWriterV1"/> class with the specified header encoder, message encryptor, and frame writer.
    /// </summary>
    /// <param name="headerEncryptor">The header encoder responsible for encoding session metadata into the header format.</param>
    /// <param name="messageEncryptor">The message encryptor responsible for encrypting the log messages using AES-GCM.</param>
    /// <param name="frameWriter">The frame writer responsible for writing the framed session data to the output stream according to the specified format.</param>
    internal SessionWriterV1(
        IHeaderEncryptor headerEncryptor,
        IMessageEncryptor messageEncryptor,
        IFrameWriter? frameWriter = null
    )
    {
        _frameWriter = frameWriter ?? new FrameWriter();
        _headerEncryptor = headerEncryptor;
        _messageEncryptor = messageEncryptor;
    }

    /// <inheritdoc />
    public void WriteSession(Stream output, SessionData session)
    {
        // 1. Encrypt header using spans
        byte[] header = _headerEncryptor.Encrypt(session.AesKey, session.Nonce, session.Timestamp);

        // 2. Calculate encrypted message length
        int encryptedMessageLength = _messageEncryptor.GetEncryptedLength(session.Plaintext.Length);

        // 3. Compute session length
        int sessionLength = header.Length + encryptedMessageLength;

        // 4. Write framing header
        _frameWriter.WriteHeader(output, 1, header, sessionLength);

        // 5. Write encrypted message length (4 bytes, big-endian)
        Span<byte> messageLengthBytes = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(
            messageLengthBytes,
            encryptedMessageLength
        );
        output.Write(messageLengthBytes);

        // 6. Encrypt and write message directly to stream (no intermediate allocation)
        _messageEncryptor.EncryptAndWrite(output, session.Plaintext, session.AesKey, session.Nonce);
    }
}
