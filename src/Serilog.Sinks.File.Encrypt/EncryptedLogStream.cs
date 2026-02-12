using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// A stream wrapper that encrypts log data before writing it to the underlying stream.
/// </summary>
public sealed class EncryptedLogStream : Stream
{
    private readonly Stream _inner;
    private readonly ISessionWriter _writer;
    private readonly MemoryStream _buffer = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptedLogStream"/> class.
    /// </summary>
    /// <param name="inner">The underlying stream to which encrypted log data will be written.</param>
    /// <param name="options">The encryption options containing the info to use for encryption.</param>
    public EncryptedLogStream(Stream inner, EncryptionOptions options)
    {
        _inner = inner;
        _writer = SessionWriterFactory.Create(options);
    }

    /// <summary>
    /// Seeking is not supported on <see cref="EncryptedLogStream"/> as it is designed for sequential writes.
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="origin"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotImplementedException();

    /// <summary>
    /// Setting length is not supported on <see cref="EncryptedLogStream"/> as it is designed for sequential writes.
    /// </summary>
    /// <param name="value"></param>
    /// <exception cref="NotImplementedException"></exception>
    public override void SetLength(long value) => throw new NotImplementedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        _buffer.Write(buffer, offset, count);
    }

    /// <summary>
    /// Reading is not supported on <see cref="EncryptedLogStream"/> as it is designed for write-only log encryption.
    /// </summary>
    public override bool CanRead => false;

    /// <summary>
    /// Seeking is not supported on <see cref="EncryptedLogStream"/> as it is designed for sequential writes.
    /// </summary>
    public override bool CanSeek => false;

    /// <summary>
    /// <see cref="EncryptedLogStream"/> supports writing log data, which is encrypted before being written to the underlying stream.
    /// </summary>
    public override bool CanWrite => true;

    /// <summary>
    /// The length of the buffered log data waiting to be encrypted and written to the underlying stream.
    /// It does not reflect the length of the underlying stream.
    /// </summary>
    public override long Length => _buffer.Length;

    /// <summary>
    /// The current position within the buffered log data.
    /// Setting the position is not supported as <see cref="EncryptedLogStream"/> is designed for sequential writes.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public override long Position
    {
        get => _buffer.Position;
        set => throw new NotImplementedException();
    }

    /// <summary>
    /// Flushes the buffered log data by encrypting it and writing it to the underlying stream. After flushing, the buffer is cleared.
    /// </summary>
    public override void Flush()
    {
        if (_buffer.Length == 0)
        {
            return;
        }

        var session = new SessionData
        {
            AesKey = RandomNumberGenerator.GetBytes(32),
            Plaintext = _buffer.ToArray(),
            Nonce = RandomNumberGenerator.GetBytes(12), // 96-bit nonce for AES-GCM
        };

        _writer.WriteSession(_inner, session);

        _inner.Flush();
        _buffer.SetLength(0);
    }

    /// <summary>
    /// Reading from <see cref="EncryptedLogStream"/> is not supported as it is designed for write-only log encryption.
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException">
    /// Reading is not supported on <see cref="EncryptedLogStream"/> as it is designed for write-only log encryption.
    /// </exception>
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotImplementedException();

    /// <summary>
    /// Disposes the stream by flushing any remaining buffered log data, encrypting it, and writing it to the underlying stream before disposing of the inner stream.
    /// </summary>
    /// <param name="disposing"></param>
    protected override void Dispose(bool disposing)
    {
        Flush();
        _inner.Dispose();
        base.Dispose(disposing);
    }
}
