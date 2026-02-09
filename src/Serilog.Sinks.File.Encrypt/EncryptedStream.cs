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
    // csharpier-ignore-start
    private static readonly byte[] _marker = [0xFF, 0xFE, 0x4C, 0x4F, 0x47, 0x48, 0x44, 0x00, 0x01];
    private const byte EscapeMarker = 0x00;
    // csharpier-ignore-end

    /// <summary>
    /// length of the nonce in bytes (counter for the stream cipher)
    /// </summary>
    private const int NonceLength = 12;

    /// <summary>
    /// length of the AES session key in bytes
    /// </summary>
    private const int SessionKeyLength = 32; // 256 bit

    /// <summary>
    /// tag (hmac) to confirm data integrity
    /// </summary>
    private const int TagLength = 16; // 128 bit

    /// <summary>
    /// Length prefix size in bytes for each encrypted block (4 bytes for int32)
    /// </summary>
    private const int LengthPrefixSize = sizeof(int);

    /// <summary>
    /// Minimum RSA key size in bits required to securely encrypt the AES session key and nonce.
    /// </summary>
    private const int MinRsaKeySize = 2048;

    private readonly Stream _underlyingStream;
    private readonly RSA _rsaPublicKey;

    private AesGcm? _aes;
    private bool _isDisposed;
    private byte[] _nonce = [0];

    /// <summary>
    /// Creates a new instance of <see cref="EncryptedStream"/> that encrypts data written to the underlying stream.
    /// </summary>
    /// <param name="underlyingStream">The stream to write encrypted data to. Must be writable.</param>
    /// <param name="rsaPublicKey">The RSA public key used to encrypt the AES-GCM session.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="underlyingStream"/> or <paramref name="rsaPublicKey"/> is null.</exception>
    /// <exception cref="CryptographicException">Thrown when RSA key operations fail.</exception>
    public EncryptedStream(Stream underlyingStream, RSA rsaPublicKey)
    {
        ArgumentNullException.ThrowIfNull(underlyingStream);
        ArgumentNullException.ThrowIfNull(rsaPublicKey);
        ArgumentOutOfRangeException.ThrowIfLessThan(rsaPublicKey.KeySize, MinRsaKeySize);

        _underlyingStream = underlyingStream;
        _rsaPublicKey = rsaPublicKey;
    }

    private void StartNewEncryptionChunk()
    {
        byte[] sessionKey = ArrayPool<byte>.Shared.Rent(SessionKeyLength);
        try
        {
            _nonce = new byte[NonceLength];

            RandomNumberGenerator.Fill(sessionKey.AsSpan(0, SessionKeyLength));
            RandomNumberGenerator.Fill(_nonce);

            _aes = new AesGcm(sessionKey.AsSpan(0, SessionKeyLength), TagLength);

            byte[] encryptedTagSize = _rsaPublicKey.Encrypt(
                BitConverter.GetBytes(TagLength),
                RSAEncryptionPadding.OaepSHA256
            );
            byte[] encryptedNonce = _rsaPublicKey.Encrypt(_nonce, RSAEncryptionPadding.OaepSHA256);
            byte[] encryptedSessionKey = _rsaPublicKey.Encrypt(
                sessionKey.AsSpan(0, SessionKeyLength).ToArray(),
                RSAEncryptionPadding.OaepSHA256
            );

            byte[] encryptedTagSizeLength = BitConverter.GetBytes(encryptedTagSize.Length);
            byte[] encryptedNonceLength = BitConverter.GetBytes(encryptedNonce.Length);
            byte[] encryptedSessionKeyLength = BitConverter.GetBytes(encryptedSessionKey.Length);

            // Combine all header data and check for markers once
            int headerUnescapedLength =
                encryptedTagSize.Length + encryptedNonce.Length + encryptedSessionKey.Length;
            int maxHeaderSize =
                headerUnescapedLength + (headerUnescapedLength / _marker.Length) + 1;
            byte[] finalHeaderBuffer = ArrayPool<byte>.Shared.Rent(maxHeaderSize);

            try
            {
                int unescapedPos = 0;
                encryptedTagSize.CopyTo(finalHeaderBuffer.AsSpan(unescapedPos));
                unescapedPos += encryptedTagSize.Length;
                encryptedNonce.CopyTo(finalHeaderBuffer.AsSpan(unescapedPos));
                unescapedPos += encryptedNonce.Length;
                encryptedSessionKey.CopyTo(finalHeaderBuffer.AsSpan(unescapedPos));

                // Fast path: check if escaping is needed
                Span<byte> unescapedHeader = finalHeaderBuffer.AsSpan(0, headerUnescapedLength);
                int markerPos = unescapedHeader.IndexOf(_marker);

                int finalHeaderLength =
                    (markerPos == -1)
                        ? headerUnescapedLength
                        : EscapeMarkersInHeader(unescapedHeader, finalHeaderBuffer);

                _underlyingStream.Write(_marker);
                _underlyingStream.Write(encryptedTagSizeLength);
                _underlyingStream.Write(encryptedNonceLength);
                _underlyingStream.Write(encryptedSessionKeyLength);
                _underlyingStream.Write(finalHeaderBuffer.AsSpan(0, finalHeaderLength));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(finalHeaderBuffer);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sessionKey.AsSpan(0, SessionKeyLength));
            ArrayPool<byte>.Shared.Return(sessionKey);
        }
    }

    private async Task StartNewEncryptionChunkAsync(CancellationToken cancellationToken = default)
    {
        byte[] sessionKey = ArrayPool<byte>.Shared.Rent(SessionKeyLength);
        try
        {
            _nonce = new byte[NonceLength];

            RandomNumberGenerator.Fill(sessionKey.AsSpan(0, SessionKeyLength));
            RandomNumberGenerator.Fill(_nonce);

            _aes = new AesGcm(sessionKey.AsSpan(0, SessionKeyLength), TagLength);

            byte[] encryptedTagSize = _rsaPublicKey.Encrypt(
                BitConverter.GetBytes(TagLength),
                RSAEncryptionPadding.OaepSHA256
            );
            byte[] encryptedNonce = _rsaPublicKey.Encrypt(_nonce, RSAEncryptionPadding.OaepSHA256);
            byte[] encryptedSessionKey = _rsaPublicKey.Encrypt(
                sessionKey.AsSpan(0, SessionKeyLength).ToArray(),
                RSAEncryptionPadding.OaepSHA256
            );

            byte[] encryptedTagSizeLength = BitConverter.GetBytes(encryptedTagSize.Length);
            byte[] encryptedNonceLength = BitConverter.GetBytes(encryptedNonce.Length);
            byte[] encryptedSessionKeyLength = BitConverter.GetBytes(encryptedSessionKey.Length);

            // Combine all header data and check for markers once
            int headerUnescapedLength =
                encryptedTagSize.Length + encryptedNonce.Length + encryptedSessionKey.Length;
            int maxHeaderSize =
                headerUnescapedLength + (headerUnescapedLength / _marker.Length) + 1;
            byte[] finalHeaderBuffer = ArrayPool<byte>.Shared.Rent(maxHeaderSize);

            try
            {
                int unescapedPos = 0;
                encryptedTagSize.CopyTo(finalHeaderBuffer.AsSpan(unescapedPos));
                unescapedPos += encryptedTagSize.Length;
                encryptedNonce.CopyTo(finalHeaderBuffer.AsSpan(unescapedPos));
                unescapedPos += encryptedNonce.Length;
                encryptedSessionKey.CopyTo(finalHeaderBuffer.AsSpan(unescapedPos));

                // Fast path: check if escaping is needed
                Span<byte> unescapedHeader = finalHeaderBuffer.AsSpan(0, headerUnescapedLength);
                int markerPos = unescapedHeader.IndexOf(_marker);

                int finalHeaderLength =
                    (markerPos == -1)
                        ? headerUnescapedLength
                        : EscapeMarkersInHeader(unescapedHeader, finalHeaderBuffer);

                await _underlyingStream
                    .WriteAsync(_marker, cancellationToken)
                    .ConfigureAwait(false);
                await _underlyingStream
                    .WriteAsync(encryptedTagSizeLength, cancellationToken)
                    .ConfigureAwait(false);
                await _underlyingStream
                    .WriteAsync(encryptedNonceLength, cancellationToken)
                    .ConfigureAwait(false);
                await _underlyingStream
                    .WriteAsync(encryptedSessionKeyLength, cancellationToken)
                    .ConfigureAwait(false);
                await _underlyingStream
                    .WriteAsync(finalHeaderBuffer.AsMemory(0, finalHeaderLength), cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(finalHeaderBuffer);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sessionKey.AsSpan(0, SessionKeyLength));
            ArrayPool<byte>.Shared.Return(sessionKey);
        }
    }

    /// <summary>
    /// Escapes marker sequences in the header data and writes the escaped result to the provided buffer.
    /// The caller should ensure there is a marker to escape first and provide a finalHeaderBuffer that is
    /// large enough to hold the escaped data (worst case is dataLen + (dataLen / makerLen) + 1).
    /// </summary>
    /// <param name="unescapedHeader"></param>
    /// <param name="finalHeaderBuffer"></param>
    /// <returns></returns>
    internal static int EscapeMarkersInHeader(Span<byte> unescapedHeader, byte[] finalHeaderBuffer)
    {
        int finalHeaderLength;
        byte[] tempBuffer = ArrayPool<byte>.Shared.Rent(unescapedHeader.Length);
        try
        {
            unescapedHeader.CopyTo(tempBuffer);
            finalHeaderLength = WriteEscapedSpan(
                tempBuffer.AsSpan(0, unescapedHeader.Length),
                finalHeaderBuffer.AsSpan(0)
            );
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(tempBuffer);
        }

        return finalHeaderLength;
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
    /// Transforms plaintext data into encrypted format with framing and escaping.
    /// </summary>
    /// <param name="plainText">The plaintext data to encrypt.</param>
    /// <returns>A tuple containing the rented buffer and the actual length of data written. Caller must return the buffer to ArrayPool.</returns>
    /// <remarks>
    /// Uses ArrayPool for all buffers to reduce allocations and GC pressure.
    /// The method performs encryption, adds integrity tags, applies escape sequences,
    /// and prepends the message length in a single-pass operation without pre-scanning.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if the aes session was not initialized.</exception>
    private (byte[] Buffer, int Length) Transform(ReadOnlySpan<byte> plainText)
    {
        if (_aes is null)
        {
            throw new InvalidOperationException("Encryption session not initialized.");
        }

        // First, encrypt the data
        int encryptedSize = plainText.Length + TagLength;
        byte[] encryptedBuffer = ArrayPool<byte>.Shared.Rent(encryptedSize);

        try
        {
            Span<byte> cypherText = encryptedBuffer.AsSpan(0, plainText.Length);
            Span<byte> hmac = encryptedBuffer.AsSpan(plainText.Length, TagLength);

            _aes.Encrypt(_nonce, plainText, cypherText, hmac);
            _nonce.IncreaseNonce();

            // Fast path: check if escaping is needed
            ReadOnlySpan<byte> encryptedData = encryptedBuffer.AsSpan(0, encryptedSize);
            int markerPos = encryptedData.IndexOf(_marker);

            byte[] resultBuffer;
            int resultSize;

            if (markerPos == -1) // No markers, no escaping needed
            {
                resultSize = LengthPrefixSize + encryptedSize;
                resultBuffer = ArrayPool<byte>.Shared.Rent(resultSize);

                BitConverter.TryWriteBytes(resultBuffer.AsSpan(0, LengthPrefixSize), encryptedSize);
                encryptedData.CopyTo(resultBuffer.AsSpan(LengthPrefixSize));
            }
            else // Markers found, need to escape
            {
                int maxEscapedSize = encryptedSize + (encryptedSize / _marker.Length) + 1;
                resultBuffer = ArrayPool<byte>.Shared.Rent(LengthPrefixSize + maxEscapedSize);

                int escapedLength = WriteEscapedSpan(
                    encryptedData,
                    resultBuffer.AsSpan(LengthPrefixSize)
                );
                BitConverter.TryWriteBytes(resultBuffer.AsSpan(0, LengthPrefixSize), escapedLength);

                resultSize = LengthPrefixSize + escapedLength;
            }

            return (resultBuffer, resultSize);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(encryptedBuffer);
        }
    }

    /// <summary>
    /// Writes a single span to the destination with escape sequences applied.
    /// </summary>
    /// <param name="source">The source data.</param>
    /// <param name="destination">The destination buffer. Up to the caller to ensure this is large enough to hold the escaped data.</param>
    /// <returns>Number of bytes written to destination.</returns>
    internal static int WriteEscapedSpan(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        int srcPos = 0;
        int destPos = 0;

        while (srcPos < source.Length)
        {
            // Find next marker occurrence in remaining source
            int markerPos = source.Slice(srcPos).IndexOf(_marker);

            if (markerPos == -1) // No more markers, copy the rest
            {
                source[srcPos..].CopyTo(destination[destPos..]);
                destPos += source.Length - srcPos;
                break;
            }

            // Copy data up to and including the marker
            int copyLength = markerPos + _marker.Length;
            source.Slice(srcPos, copyLength).CopyTo(destination[destPos..]);
            srcPos += copyLength;
            destPos += copyLength;

            // Add escape byte after the marker
            destination[destPos] = EscapeMarker;
            destPos++;
        }

        return destPos;
    }
}
