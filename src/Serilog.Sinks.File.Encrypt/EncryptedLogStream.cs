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
    private readonly byte[] _aesKey = new byte[32]; // Reusable buffer for AES key
    private readonly byte[] _nonce = new byte[12]; // Reusable buffer for nonce
    private AesGcm? _aesGcm; // Reusable AES-GCM instance

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptedLogStream"/> class.
    /// </summary>
    /// <param name="inner">The underlying stream to which encrypted log data will be written.</param>
    /// <param name="options">The encryption options containing the info to use for encryption.</param>
    /// <exception cref="ArgumentNullException">Thrown if either the inner stream or encryption options are null.</exception>
    public EncryptedLogStream(Stream inner, EncryptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(options);
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
        if (_aesGcm == null)
        {
            StartNewSession();
        }
        Write(buffer[offset..(offset + count)].AsSpan());
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        if (_aesGcm == null)
        {
            StartNewSession();
        }
        SessionData session = new SessionData
        {
            AesGcm = _aesGcm!,
            AesKey = _aesKey,
            Nonce = _nonce,
        };
        _writer.WriteSession(_inner, session, buffer);
    }

    private void StartNewSession()
    {
        // Generate random values directly into reusable buffers (no allocation)
        RandomNumberGenerator.Fill(_aesKey);
        RandomNumberGenerator.Fill(_nonce);
        _aesGcm = new AesGcm(_aesKey, EncryptionConstants.TagLength);
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
    /// The length of the underlying stream. This may not reflect the actual length of the encrypted log data until after flushing, as data is buffered for encryption.
    /// </summary>
    public override long Length => _inner.Length;

    /// <summary>
    /// The current position within the buffered log data.
    /// Setting the position is not supported as <see cref="EncryptedLogStream"/> is designed for sequential writes.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public override long Position
    {
        get => _inner.Position;
        set => throw new NotImplementedException();
    }

    /// <summary>
    /// Flushes the buffered log data by encrypting it and writing it to the underlying stream. After flushing, the buffer is cleared.
    /// </summary>
    public override void Flush()
    {
        _inner.Flush();
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
        if (disposing)
        {
            Flush();
            _inner.Dispose();
            _aesGcm?.Dispose();
        }
        base.Dispose(disposing);
    }
}
