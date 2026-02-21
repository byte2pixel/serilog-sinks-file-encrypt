using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// A stream wrapper that encrypts log data before writing it to the underlying stream.
/// Uses a hybrid encryption scheme: RSA for session key exchange (once per session),
/// AES-GCM for data encryption (per message).
/// </summary>
public sealed class LogWriter : Stream
{
    private readonly Stream _inner;
    private readonly ISessionHeaderWriter _headerWriter;

    /// <summary>
    /// Reusable buffer for AES key
    /// </summary>
    private readonly byte[] _aesKey = new byte[EncryptionConstants.SessionKeyLength];

    /// <summary>
    /// Reusable buffer for nonce
    /// </summary>
    private readonly byte[] _nonce = new byte[EncryptionConstants.NonceLength];

    /// <summary>
    /// Reusable AES-GCM instance
    /// </summary>
    private AesGcm? _aesGcm;

    private bool _sessionHeaderWritten;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogWriter"/> class.
    /// </summary>
    /// <param name="inner">The underlying stream to which encrypted log data will be written.</param>
    /// <param name="options">The encryption options containing the info to use for encryption.</param>
    /// <exception cref="ArgumentNullException">Thrown if either the inner stream or encryption options are null.</exception>
    public LogWriter(Stream inner, EncryptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(options);
        _inner = inner;
        _headerWriter = SessionHeaderWriterFactory.Create(options);
    }

    /// <summary>
    /// Seeking is not supported on <see cref="LogWriter"/> as it is designed for sequential writes.
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="origin"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <summary>
    /// Setting length is not supported on <see cref="LogWriter"/> as it is designed for sequential writes.
    /// </summary>
    /// <param name="value"></param>
    /// <exception cref="NotSupportedException"></exception>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        Write(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        _aesGcm ??= StartNewSession();

        // Write session header only once per session
        if (!_sessionHeaderWritten)
        {
            _headerWriter.WriteHeader(_inner, _aesKey, _nonce);
            _sessionHeaderWritten = true;
        }

        int plaintextLength = buffer.Length;
        int encryptedPayloadLength = plaintextLength + EncryptionConstants.TagLength;

        // Rent buffers from pool
        byte[] ciphertext = ArrayPool<byte>.Shared.Rent(plaintextLength);
        byte[] tag = ArrayPool<byte>.Shared.Rent(EncryptionConstants.TagLength);

        try
        {
            // Encrypt directly into pooled buffers
            _aesGcm.Encrypt(
                _nonce,
                buffer,
                ciphertext.AsSpan(0, plaintextLength),
                tag.AsSpan(0, EncryptionConstants.TagLength),
                associatedData: null
            );

            _nonce.IncreaseNonce();

            // Write 4-byte length prefix (big-endian) for self-framing
            Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(lengthBytes, encryptedPayloadLength);
            _inner.Write(lengthBytes);

            // Write encrypted data directly to stream from pooled buffers
            _inner.Write(ciphertext, 0, plaintextLength);
            _inner.Write(tag, 0, EncryptionConstants.TagLength);
        }
        finally
        {
            // Clear and return buffers to pool
            Array.Clear(ciphertext, 0, plaintextLength);
            ArrayPool<byte>.Shared.Return(ciphertext);
            ArrayPool<byte>.Shared.Return(tag);
        }
    }

    private AesGcm StartNewSession()
    {
        // Generate random values directly into reusable buffers (no allocation)
        RandomNumberGenerator.Fill(_aesKey);
        RandomNumberGenerator.Fill(_nonce);
        return new AesGcm(_aesKey, EncryptionConstants.TagLength);
    }

    /// <summary>
    /// Reading is not supported on <see cref="LogWriter"/> as it is designed for write-only log encryption.
    /// </summary>
    public override bool CanRead => false;

    /// <summary>
    /// Seeking is not supported on <see cref="LogWriter"/> as it is designed for sequential writes.
    /// </summary>
    public override bool CanSeek => false;

    /// <summary>
    /// <see cref="LogWriter"/> supports writing log data, which is encrypted before being written to the underlying stream.
    /// </summary>
    public override bool CanWrite => true;

    /// <summary>
    /// The length of the underlying stream. This may not reflect the actual length of the encrypted log data until after flushing, as data is buffered for encryption.
    /// </summary>
    public override long Length => _inner.Length;

    /// <summary>
    /// The current position within the buffered log data.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Setting the position is not supported on <see cref="LogWriter"/> as it is designed for sequential writes.
    /// </exception>
    public override long Position
    {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Flushes the buffered log data by encrypting it and writing it to the underlying stream. After flushing, the buffer is cleared.
    /// </summary>
    public override void Flush()
    {
        _inner.Flush();
    }

    /// <summary>
    /// Reading from <see cref="LogWriter"/> is not supported as it is designed for write-only log encryption.
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException">
    /// Reading is not supported on <see cref="LogWriter"/> as it is designed for write-only log encryption.
    /// </exception>
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

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
