using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Writers;

/// <summary>
/// Session writer for version 1 of the encrypted log format.
/// This implementation handles the encoding of session metadata into the header format using RSA encryption,
/// encrypts log messages using AES-GCM, and writes the framed session data to the output stream according to
/// the specified format for version 1.
/// </summary>
public sealed class SessionWriterV1 : ISessionWriter
{
    private readonly IHeaderEncoder _headerEncoder;
    private readonly IMessageEncryptor _messageEncryptor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionWriterV1"/> class with the specified header encoder, message encryptor, and frame writer.
    /// </summary>
    /// <param name="headerEncoder">The header encoder responsible for encoding session metadata into the header format.</param>
    /// <param name="messageEncryptor">The message encryptor responsible for encrypting the log messages using AES-GCM.</param>
    public SessionWriterV1(IHeaderEncoder headerEncoder, IMessageEncryptor messageEncryptor)
    {
        _headerEncoder = headerEncoder;
        _messageEncryptor = messageEncryptor;
    }

    /// <inheritdoc />
    public void WriteSession(Stream output, SessionData session)
    {
        // 1. Encode header (RSA)
        byte[] header = _headerEncoder.EncodeHeader(session);
        // 2. Encrypt logs
        EncryptedMessage encryptedMessages = _messageEncryptor.Encrypt(
            session.Plaintext,
            session.AesKey,
            session.Nonce
        );
        // 3. Compute session length
        int sessionLength = header.Length + encryptedMessages.TotalLength;
        // 4. Write framing
        FrameWriter.WriteHeader(output, version: 1, header, sessionLength);
        // 5. Write encrypted logs
        FrameWriter.WriteMessage(output, encryptedMessages);
    }
}
