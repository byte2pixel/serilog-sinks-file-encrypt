using System.Buffers;
using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Encrypts data written to the underlying stream using hybrid AES-256 GCM + RSA encryption.
/// </summary>
/// <remarks>
/// <para>
/// This stream wraps an underlying stream and encrypts all data written to it using a hybrid encryption scheme:
/// - AES-256 GCM is used for data encryption (symmetric, fast)
/// - RSA is used to encrypt the AES key and nonce (asymmetric, secure key exchange)
/// </para>
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
    private const int TagLength = 12; // 96 bit

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

        _underlyingStream = underlyingStream;
        _rsaPublicKey = rsaPublicKey;
    }

    private void StartNewEncryptionChunk()
    {
        byte[] sessionKey = new byte[SessionKeyLength];
        _nonce = new byte[NonceLength];

        RandomNumberGenerator.Fill(sessionKey);
        RandomNumberGenerator.Fill(_nonce);

        _aes = new AesGcm(sessionKey, TagLength);

        byte[] encryptedTagSize = _rsaPublicKey.Encrypt(
            BitConverter.GetBytes(TagLength),
            RSAEncryptionPadding.OaepSHA256
        );
        byte[] encryptedNonce = _rsaPublicKey.Encrypt(_nonce, RSAEncryptionPadding.OaepSHA256);
        byte[] encryptedSessionKey = _rsaPublicKey.Encrypt(
            sessionKey,
            RSAEncryptionPadding.OaepSHA256
        );

        byte[] encryptedTagSizeLength = BitConverter.GetBytes(encryptedTagSize.Length);
        byte[] encryptedNonceLength = BitConverter.GetBytes(encryptedNonce.Length);
        byte[] encryptedSessionKeyLength = BitConverter.GetBytes(encryptedSessionKey.Length);

        // Combine all header data and check for markers once
        int headerUnescapedLength =
            encryptedTagSize.Length + encryptedNonce.Length + encryptedSessionKey.Length;
        int maxHeaderSize = headerUnescapedLength + (headerUnescapedLength / _marker.Length) + 1;
        byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(maxHeaderSize);

        try
        {
            int unescapedPos = 0;
            encryptedTagSize.CopyTo(headerBuffer.AsSpan(unescapedPos));
            unescapedPos += encryptedTagSize.Length;
            encryptedNonce.CopyTo(headerBuffer.AsSpan(unescapedPos));
            unescapedPos += encryptedNonce.Length;
            encryptedSessionKey.CopyTo(headerBuffer.AsSpan(unescapedPos));

            // Fast path: check if escaping is needed
            Span<byte> unescapedHeader = headerBuffer.AsSpan(0, headerUnescapedLength);
            int markerPos = unescapedHeader.IndexOf(_marker);

            int finalHeaderLength;
            if (markerPos == -1) // No markers, no escaping needed
            {
                finalHeaderLength = headerUnescapedLength;
            }
            else
            {
                // Markers found, escape needed, rent a buffer.
                byte[] tempBuffer = ArrayPool<byte>.Shared.Rent(headerUnescapedLength);
                try
                {
                    unescapedHeader.CopyTo(tempBuffer);
                    finalHeaderLength = WriteEscapedSpan(
                        tempBuffer.AsSpan(0, headerUnescapedLength),
                        headerBuffer.AsSpan(0)
                    );
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(tempBuffer);
                }
            }

            _underlyingStream.Write(_marker);
            _underlyingStream.Write(encryptedTagSizeLength);
            _underlyingStream.Write(encryptedNonceLength);
            _underlyingStream.Write(encryptedSessionKeyLength);
            _underlyingStream.Write(headerBuffer.AsSpan(0, finalHeaderLength));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuffer);
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

        if (_aes is null)
        {
            StartNewEncryptionChunk(); // Write the RSA-encrypted header for the new AES session
        }

        _underlyingStream.Write(Transform(buffer));
    }

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_aes is null)
        {
            StartNewEncryptionChunk(); // Write the RSA-encrypted header for the new AES session
        }

        await _underlyingStream
            .WriteAsync(Transform(buffer.Span), cancellationToken)
            .ConfigureAwait(false);
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
    /// This stream does not support getting or setting the position.
    /// </summary>
    /// <exception cref="NotSupportedException"></exception>
    public override long Position
    {
        get => throw new NotSupportedException();
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
    /// <returns>Encrypted data with length prefix and escaping applied.</returns>
    /// <remarks>
    /// Uses ArrayPool for temporary buffers to reduce allocations and GC pressure.
    /// The method performs encryption, adds integrity tags, applies escape sequences,
    /// and prepends the message length in a single-pass operation without pre-scanning.
    /// </remarks>
    private byte[] Transform(ReadOnlySpan<byte> plainText)
    {
        // First, encrypt the data
        int encryptedSize = plainText.Length + TagLength;
        byte[] encryptedBuffer = ArrayPool<byte>.Shared.Rent(encryptedSize);

        try
        {
            Span<byte> cypherText = encryptedBuffer.AsSpan(0, plainText.Length);
            Span<byte> hmac = encryptedBuffer.AsSpan(plainText.Length, TagLength);

            _aes!.Encrypt(_nonce, plainText, cypherText, hmac);
            _nonce.IncreaseNonce();

            // Fast path: check if escaping is needed
            ReadOnlySpan<byte> encryptedData = encryptedBuffer.AsSpan(0, encryptedSize);
            int markerPos = encryptedData.IndexOf(_marker);

            if (markerPos == -1) // No markers, no escaping needed
            {
                byte[] result = new byte[sizeof(int) + encryptedSize];
                BitConverter.TryWriteBytes(result.AsSpan(0, sizeof(int)), encryptedSize);
                encryptedData.CopyTo(result.AsSpan(sizeof(int)));
                return result;
            }

            // Markers found, need to escape
            int maxEscapedSize = encryptedSize + (encryptedSize / _marker.Length) + 1;
            byte[] escapedBuffer = ArrayPool<byte>.Shared.Rent(sizeof(int) + maxEscapedSize);

            try
            {
                int escapedLength = WriteEscapedSpan(
                    encryptedData,
                    escapedBuffer.AsSpan(sizeof(int))
                );
                BitConverter.TryWriteBytes(escapedBuffer.AsSpan(0, sizeof(int)), escapedLength);

                byte[] result = new byte[sizeof(int) + escapedLength];
                escapedBuffer.AsSpan(0, sizeof(int) + escapedLength).CopyTo(result);
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(escapedBuffer);
            }
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
            int markerPos = source[srcPos..].IndexOf(_marker);

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
