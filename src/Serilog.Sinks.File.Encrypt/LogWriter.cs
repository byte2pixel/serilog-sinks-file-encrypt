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
    private readonly ISessionWriter _writer;

    /// <summary>
    /// Reusable buffer for AES key
    /// </summary>
    private readonly byte[] _aesKey = new byte[EncryptionConstants.SessionKeyLength];

    /// <summary>
    /// Reusable buffer for nonce
    /// </summary>
    private readonly byte[] _nonce = new byte[EncryptionConstants.NonceLength];

    /// <summary>
    /// Reserved nonce for the end-of-log seal record: the initial session nonce with its
    /// 64-bit counter decremented by one. Independent of the number of data frames written,
    /// so the decryptor can authenticate the seal even when trailing frames are missing.
    /// </summary>
    private readonly byte[] _sealNonce = new byte[EncryptionConstants.NonceLength];

    /// <summary>
    /// Reusable associated-data buffer bound into every AES-GCM record:
    /// headerHash(32) + frameSequence(8, big-endian) + frameType(1).
    /// The header hash is filled once per session; only the last 9 bytes change per record.
    /// </summary>
    private readonly byte[] _aad = new byte[EncryptionConstants.AadLength];

    /// <summary>
    /// Number of data frames written in the current session. Bound into each frame's
    /// associated data as the frame sequence and carried in the seal record's payload.
    /// </summary>
    private ulong _frameCount;

    /// <summary>
    /// Reusable AES-GCM instance
    /// </summary>
    private AesGcm? _aesGcm;

    private bool _sessionHeaderWritten;
    private bool _disposed;

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
        _writer = new SessionWriter(new HeaderWriter(options), options.KeyId);
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

        // Write session header only once per session; its SHA-256 hash becomes the
        // session-binding prefix of every record's associated data.
        if (!_sessionHeaderWritten)
        {
            _writer.WriteHeader(
                _inner,
                _aesKey,
                _nonce,
                _aad.AsSpan(0, EncryptionConstants.HeaderHashLength)
            );
            _sessionHeaderWritten = true;
        }

        int plaintextLength = buffer.Length;
        int encryptedPayloadLength = plaintextLength + EncryptionConstants.TagLength;

        // Rent buffers from pool
        byte[] ciphertext = ArrayPool<byte>.Shared.Rent(plaintextLength);
        Span<byte> tag = stackalloc byte[EncryptionConstants.TagLength];

        try
        {
            // Bind the frame's position and type into the associated data so that dropped,
            // reordered, duplicated, or cross-session-spliced frames fail authentication.
            BinaryPrimitives.WriteUInt64BigEndian(
                _aad.AsSpan(EncryptionConstants.HeaderHashLength),
                _frameCount
            );
            _aad[EncryptionConstants.AadLength - 1] = EncryptionConstants.FrameTypeData;

            // Encrypt directly into pooled buffers
            _aesGcm.Encrypt(
                _nonce,
                buffer,
                ciphertext.AsSpan(0, plaintextLength),
                tag,
                associatedData: _aad
            );

            _frameCount++;
            _nonce.IncreaseNonce();

            // Write 4-byte length prefix (big-endian) for self-framing
            Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(lengthBytes, encryptedPayloadLength);
            _inner.Write(lengthBytes);

            // Write encrypted data directly to stream from pooled buffers
            _inner.Write(ciphertext, 0, plaintextLength);
            _inner.Write(tag);
        }
        finally
        {
            // Clear and return buffers to pool
            Array.Clear(ciphertext, 0, plaintextLength);
            ArrayPool<byte>.Shared.Return(ciphertext);
        }
    }

    private AesGcm StartNewSession()
    {
        // Generate random values directly into reusable buffers (no allocation)
        RandomNumberGenerator.Fill(_aesKey);
        RandomNumberGenerator.Fill(_nonce);

        // Reserve the nonce just below the initial counter for the seal record. Data frames
        // count upward from the initial nonce, so this value is never reused for a frame
        // (unless a session exceeds 2^64 - 1 frames, the documented counter wrap bound).
        _nonce.CopyTo(_sealNonce, 0);
        _sealNonce.DecreaseNonce();

        return new AesGcm(_aesKey, EncryptionConstants.TagLength);
    }

    /// <summary>
    /// Writes the end-of-log seal record: a 4-byte marker followed by the AES-GCM-encrypted
    /// final frame count. The seal is authenticated with the session's header hash and the
    /// seal frame type as associated data, using the reserved seal nonce. Its presence lets
    /// the decryptor distinguish a cleanly closed (sealed) session from a truncated or
    /// crashed (unsealed) one, and its frame count makes tail truncation detectable.
    /// No-op if the session never wrote a header.
    /// </summary>
    private void WriteSeal()
    {
        if (!_sessionHeaderWritten || _aesGcm is null)
        {
            return;
        }

        // Seal associated data: headerHash (already in place) + zeroed sequence field + seal type.
        // The frame count travels in the encrypted payload, not the AAD, so a count mismatch is
        // reportable as truncation rather than failing as opaque tampering.
        _aad.AsSpan(EncryptionConstants.HeaderHashLength, EncryptionConstants.FrameSequenceLength)
            .Clear();
        _aad[EncryptionConstants.AadLength - 1] = EncryptionConstants.FrameTypeSeal;

        Span<byte> plaintext = stackalloc byte[EncryptionConstants.SealPlaintextLength];
        BinaryPrimitives.WriteUInt64BigEndian(plaintext, _frameCount);

        Span<byte> seal =
            stackalloc byte[
                EncryptionConstants.SealMarkerBytes.Length
                    + EncryptionConstants.SealRecordRemainderLength
            ];
        EncryptionConstants.SealMarkerBytes.CopyTo(seal);
        _aesGcm.Encrypt(
            _sealNonce,
            plaintext,
            seal.Slice(
                EncryptionConstants.SealMarkerBytes.Length,
                EncryptionConstants.SealPlaintextLength
            ),
            seal.Slice(
                EncryptionConstants.SealMarkerBytes.Length
                    + EncryptionConstants.SealPlaintextLength,
                EncryptionConstants.TagLength
            ),
            _aad
        );

        // Single write keeps the window for a partially persisted seal as small as possible.
        _inner.Write(seal);
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
    /// The length, in bytes, of the underlying (encrypted) stream. Each write is encrypted and forwarded to the
    /// underlying stream immediately, so this reflects the encrypted bytes written so far, not the plaintext length.
    /// </summary>
    public override long Length => _inner.Length;

    /// <summary>
    /// The current position within the underlying (encrypted) stream.
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
    /// Flushes the underlying stream. Each write is encrypted and forwarded immediately, so the writer itself
    /// buffers no plaintext; this simply flushes the underlying stream.
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
    /// Writes the end-of-log seal record, then flushes and disposes the underlying stream,
    /// disposes the AES-GCM instance, and wipes the session key and nonces from memory.
    /// No plaintext is buffered by the writer, so there is nothing further to encrypt.
    /// If writing the seal fails (e.g. the disk is full), the stream is still disposed and key
    /// material still wiped; the file then simply ends unsealed, which the decryptor reports.
    /// </summary>
    /// <param name="disposing"></param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            try
            {
                try
                {
                    WriteSeal();
                }
                finally
                {
                    Flush();
                    _inner.Dispose();
                    _aesGcm?.Dispose();
                }
            }
            finally
            {
                // Wipe the session key and nonces so they do not linger in managed memory,
                // even if sealing, flushing, or disposing the inner stream throws.
                CryptographicOperations.ZeroMemory(_aesKey);
                CryptographicOperations.ZeroMemory(_nonce);
                CryptographicOperations.ZeroMemory(_sealNonce);
            }
        }
        base.Dispose(disposing);
    }
}
