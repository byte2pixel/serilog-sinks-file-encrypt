using System.Buffers;
using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// <para>
/// EncryptedStream is a write-only stream that encrypts data using a hybrid encryption scheme combining AES-256 GCM
/// for data encryption and RSA for secure key exchange. It is designed to be used with Serilog.Sinks.File to provide
/// transparent encryption of log files while maintaining performance and security.
/// </para>
/// <para>
/// Each call to Flush() finalizes the current encryption chunk, allowing for efficient memory usage and secure
/// handling of log data.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Memory Usage:</b> Each call to <see cref="Flush"/> or <see cref="FlushAsync"/> creates a new encryption chunk.
/// A chunk consists of one RSA header (symmetric AES key) and multiple AES-encrypted data blocks.
/// </para>
/// <para>
/// <b>Thread Safety:</b> This class is NOT thread-safe. It is designed to be used by Serilog.Sinks.File
/// which handles thread synchronization internally. Do not share instances across threads.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var rsa = RSA.Create();
/// rsa.FromXmlString(publicKeyXml);
///
/// using FileStream fileStream = File.OpenWrite("encrypted.log");
/// using EncryptedStream encryptedStream = new(fileStream, rsa);
///
/// byte[] data = Encoding.UTF8.GetBytes("Log entry");
/// encryptedStream.Write(data, 0, data.Length);
/// encryptedStream.Flush(); // Finalizes the encryption chunk
/// </code>
/// </example>
public class EncryptedStream : Stream
{
    private readonly Stream _underlyingStream;
    private readonly RSA _publicKey;

    private AesGcm? _aes;
    private bool _isDisposed;
    private byte[] _nonce = [0];

    /// <summary>
    /// Creates a new instance of <see cref="EncryptedStream"/> that encrypts data written to the underlying stream.
    /// </summary>
    /// <param name="underlyingStream">The stream to write encrypted data to. Must be writable.</param>
    /// <param name="publicKey">The RSA public key used to encrypt the AES-GCM session.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="underlyingStream"/> or <paramref name="publicKey"/> is null.</exception>
    /// <exception cref="CryptographicException">Thrown when RSA key operations fail.</exception>
    public EncryptedStream(Stream underlyingStream, RSA publicKey)
    {
        ArgumentNullException.ThrowIfNull(underlyingStream);
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            publicKey.KeySize,
            EncryptionConstants.MinRsaKeySize
        );

        _underlyingStream = underlyingStream;
        _publicKey = publicKey;
    }

    private void StartNewEncryptionChunk()
    {
        byte[] header = ArrayPool<byte>.Shared.Rent(EncryptionConstants.HeaderUnencryptedSize);
        try
        {
            _nonce = new byte[EncryptionConstants.NonceLength];

            RandomNumberGenerator.Fill(
                header.AsSpan(
                    EncryptionConstants.HeaderSessionKeyOffset,
                    EncryptionConstants.SessionKeyLength
                )
            );
            RandomNumberGenerator.Fill(_nonce);

            _aes = new AesGcm(
                header.AsSpan(
                    EncryptionConstants.HeaderSessionKeyOffset,
                    EncryptionConstants.SessionKeyLength
                ),
                EncryptionConstants.TagLength
            );

            BitConverter.GetBytes(EncryptionConstants.TagLength).CopyTo(header, 0);
            _nonce.CopyTo(header, EncryptionConstants.HeaderNonceOffset);

            byte[] encryptedRsaHeader = _publicKey.Encrypt(
                header.AsSpan(0, EncryptionConstants.HeaderUnencryptedSize).ToArray(),
                RSAEncryptionPadding.OaepSHA256
            );

            _underlyingStream.Write(EncryptionConstants.Marker);
            _underlyingStream.Write(EncryptionConstants.Version);
            _underlyingStream.Write(encryptedRsaHeader);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                header.AsSpan(0, EncryptionConstants.HeaderUnencryptedSize)
            );
            ArrayPool<byte>.Shared.Return(header);
        }
    }

    private async Task StartNewEncryptionChunkAsync(CancellationToken cancellationToken = default)
    {
        byte[] header = ArrayPool<byte>.Shared.Rent(EncryptionConstants.HeaderUnencryptedSize);
        try
        {
            _nonce = new byte[EncryptionConstants.NonceLength];

            RandomNumberGenerator.Fill(
                header.AsSpan(
                    EncryptionConstants.HeaderSessionKeyOffset,
                    EncryptionConstants.SessionKeyLength
                )
            );
            RandomNumberGenerator.Fill(_nonce);

            _aes = new AesGcm(
                header.AsSpan(
                    EncryptionConstants.HeaderSessionKeyOffset,
                    EncryptionConstants.SessionKeyLength
                ),
                EncryptionConstants.TagLength
            );

            BitConverter.GetBytes(EncryptionConstants.TagLength).CopyTo(header, 0);
            _nonce.CopyTo(header, EncryptionConstants.HeaderNonceOffset);

            byte[] encryptedRsaHeader = _publicKey.Encrypt(
                header.AsSpan(0, EncryptionConstants.HeaderUnencryptedSize).ToArray(),
                RSAEncryptionPadding.OaepSHA256
            );

            await _underlyingStream
                .WriteAsync(EncryptionConstants.Marker, cancellationToken)
                .ConfigureAwait(false);
            await _underlyingStream
                .WriteAsync(EncryptionConstants.Version, cancellationToken)
                .ConfigureAwait(false);
            await _underlyingStream
                .WriteAsync(encryptedRsaHeader, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                header.AsSpan(0, EncryptionConstants.HeaderUnencryptedSize)
            );
            ArrayPool<byte>.Shared.Return(header);
        }
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        _underlyingStream.Flush();
    }

    /// <inheritdoc/>
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await _underlyingStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// This stream does not support reading.
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// This stream does not support seeking.
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="origin"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// This stream does not support setting length.
    /// </summary>
    /// <param name="value"></param>
    /// <exception cref="NotSupportedException"></exception>
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        Write(buffer[offset..(offset + count)].AsSpan());
    }

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (buffer.Length == 0)
        {
            return;
        }

        if (_aes is null)
        {
            StartNewEncryptionChunk(); // Write the RSA-encrypted header for the new AES session
        }

        (byte[] resultBuffer, int resultLength) = Transform(buffer);
        try
        {
            _underlyingStream.Write(resultBuffer.AsSpan(0, resultLength));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(resultBuffer);
        }
    }

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (buffer.Span.Length == 0)
        {
            return;
        }

        if (_aes is null)
        {
            await StartNewEncryptionChunkAsync(cancellationToken).ConfigureAwait(false);
        }

        (byte[] resultBuffer, int resultLength) = Transform(buffer.Span);
        try
        {
            await _underlyingStream
                .WriteAsync(resultBuffer.AsMemory(0, resultLength), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(resultBuffer);
        }
    }

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <summary>
    /// This stream does not support setting the position.
    /// The getter returns the total number of bytes written to the write only stream.
    /// </summary>
    /// <exception cref="NotSupportedException"></exception>
    public override long Position
    {
        get => _underlyingStream.Position;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (disposing)
        {
            // Flush any remaining data before disposing
            if (_aes is not null)
            {
                try
                {
                    Flush();
                }
                catch (ObjectDisposedException)
                {
                    // Underlying stream already disposed - safe to ignore during disposal
                }
                catch (IOException)
                {
                    // I/O error during flush - safe to ignore during disposal
                }
                catch (CryptographicException)
                {
                    // Encryption finalization error - safe to ignore during disposal
                }
                finally
                {
                    _aes.Dispose();
                }
            }

            _underlyingStream.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Transforms plaintext data into encrypted format with framing.
    /// </summary>
    /// <param name="plainText">The plaintext data to encrypt.</param>
    /// <returns>A tuple containing the rented buffer and the actual length of data written.
    /// Caller must only write the length and Caller must return the buffer to ArrayPool.
    /// </returns>
    /// <remarks>
    /// Uses ArrayPool for all buffers to reduce allocations and GC pressure.
    /// The method performs encryption, adds integrity tags,
    /// and prepends the message length in a single-pass operation.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if the aes session was not initialized.</exception>
    private (byte[] Buffer, int Length) Transform(ReadOnlySpan<byte> plainText)
    {
        if (_aes is null)
        {
            throw new InvalidOperationException("Encryption session not initialized.");
        }

        int encryptedSize = plainText.Length + EncryptionConstants.TagLength;
        byte[] encryptedBuffer = ArrayPool<byte>.Shared.Rent(encryptedSize);

        try
        {
            Span<byte> cypherText = encryptedBuffer.AsSpan(0, plainText.Length);
            Span<byte> hmac = encryptedBuffer.AsSpan(
                plainText.Length,
                EncryptionConstants.TagLength
            );

            _aes.Encrypt(_nonce, plainText, cypherText, hmac);
            _nonce.IncreaseNonce();

            ReadOnlySpan<byte> encryptedData = encryptedBuffer.AsSpan(0, encryptedSize);
            int resultSize = EncryptionConstants.SizeOfInt + encryptedSize;
            byte[] resultBuffer = ArrayPool<byte>.Shared.Rent(resultSize);

            BitConverter.TryWriteBytes(
                resultBuffer.AsSpan(0, EncryptionConstants.SizeOfInt),
                encryptedSize
            );
            encryptedData.CopyTo(resultBuffer.AsSpan(EncryptionConstants.SizeOfInt));

            return (resultBuffer, resultSize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptedBuffer);
            ArrayPool<byte>.Shared.Return(encryptedBuffer);
        }
    }
}
