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
    /// <param name="rsaPublicKey">The RSA public key used to encrypt the AES session key and IV. Minimum 2048-bit key size recommended.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="underlyingStream"/> or <paramref name="rsaPublicKey"/> is null.</exception>
    /// <exception cref="CryptographicException">Thrown when RSA key operations fail.</exception>
    /// <remarks>
    /// The constructor immediately writes an encryption header containing the RSA-encrypted AES key and IV to the underlying stream.
    /// This header is required for decryption and cannot be removed without corrupting the file.
    /// </remarks>
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

        // Calculate escaped header size
        int headerUnescapedLength =
            encryptedTagSize.Length + encryptedNonce.Length + encryptedSessionKey.Length;
        int escapeCount =
            CountMarkerOccurrences(encryptedTagSize)
            + CountMarkerOccurrences(encryptedNonce)
            + CountMarkerOccurrences(encryptedSessionKey);
        int headerEscapedLength = headerUnescapedLength + escapeCount;

        // Allocate and write escaped header
        byte[] messageHeader = new byte[headerEscapedLength];
        int pos = 0;
        pos += WriteEscapedSpan(encryptedTagSize, messageHeader.AsSpan(pos));
        pos += WriteEscapedSpan(encryptedNonce, messageHeader.AsSpan(pos));
        WriteEscapedSpan(encryptedSessionKey, messageHeader.AsSpan(pos));

        _underlyingStream.Write(_marker);
        _underlyingStream.Write(encryptedTagSizeLength);
        _underlyingStream.Write(encryptedNonceLength);
        _underlyingStream.Write(encryptedSessionKeyLength);
        _underlyingStream.Write(messageHeader);
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

        if (_aes == null)
        {
            StartNewEncryptionChunk();
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

        if (_aes == null)
        {
            StartNewEncryptionChunk();
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
            if (_aes != null)
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
    /// and prepends the message length in a single optimized pass.
    /// </remarks>
    private byte[] Transform(ReadOnlySpan<byte> plainText)
    {
        byte[] cipherTextBuffer = ArrayPool<byte>.Shared.Rent(plainText.Length);
        byte[] hmacBuffer = ArrayPool<byte>.Shared.Rent(TagLength);

        try
        {
            Span<byte> cypherText = cipherTextBuffer.AsSpan(0, plainText.Length);
            Span<byte> hmac = hmacBuffer.AsSpan(0, TagLength);

            _aes!.Encrypt(_nonce, plainText, cypherText, hmac);
            _nonce.IncreaseNonce();

            int messageLength = plainText.Length + TagLength;
            int escapedLength = CalculateEscapedLength(cypherText, hmac, messageLength);

            byte[] result = new byte[sizeof(int) + escapedLength];
            BitConverter.TryWriteBytes(result.AsSpan(0, sizeof(int)), escapedLength);
            WriteEscaped(cypherText, hmac, result.AsSpan(sizeof(int)));
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(cipherTextBuffer);
            ArrayPool<byte>.Shared.Return(hmacBuffer);
        }
    }

    /// <summary>
    /// Calculates the final length after escaping marker sequences.
    /// </summary>
    /// <param name="cipherText">The encrypted data.</param>
    /// <param name="hmac">The HMAC tag.</param>
    /// <param name="unescapedLength">The combined length before escaping.</param>
    /// <returns>The length including escape bytes.</returns>
    private static int CalculateEscapedLength(
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> hmac,
        int unescapedLength
    )
    {
        int escapeCount = 0;
        escapeCount += CountMarkerOccurrences(cipherText);
        escapeCount += CountMarkerOccurrences(hmac);
        return unescapedLength + escapeCount;
    }

    /// <summary>
    /// Counts how many times the marker sequence appears in the data.
    /// </summary>
    /// <param name="data">The data to scan.</param>
    /// <returns>Number of marker occurrences.</returns>
    private static int CountMarkerOccurrences(ReadOnlySpan<byte> data)
    {
        int count = 0;
        int searchStart = 0;

        while (searchStart <= data.Length - _marker.Length)
        {
            int pos = data[searchStart..].IndexOf(_marker);
            if (pos == -1)
            {
                break;
            }

            count++;
            searchStart += pos + 1; // Move past this occurrence
        }

        return count;
    }

    /// <summary>
    /// Writes cipher text and HMAC to the destination buffer with escape sequences applied.
    /// Uses a single-pass algorithm that writes escaped data directly without intermediate allocations.
    /// </summary>
    /// <param name="cipherText">The encrypted data.</param>
    /// <param name="hmac">The HMAC tag.</param>
    /// <param name="destination">The destination buffer (must be sized correctly).</param>
    private static void WriteEscaped(
        ReadOnlySpan<byte> cipherText,
        ReadOnlySpan<byte> hmac,
        Span<byte> destination
    )
    {
        int destPos = 0;
        destPos += WriteEscapedSpan(cipherText, destination[destPos..]);
        WriteEscapedSpan(hmac, destination[destPos..]);
    }

    /// <summary>
    /// Writes a single span to the destination with escape sequences applied.
    /// </summary>
    /// <param name="source">The source data.</param>
    /// <param name="destination">The destination buffer.</param>
    /// <returns>Number of bytes written to destination.</returns>
    private static int WriteEscapedSpan(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        int srcPos = 0;
        int destPos = 0;

        while (srcPos < source.Length)
        {
            // Find next marker occurrence
            int remaining = source.Length - srcPos;
            int searchLength = Math.Min(remaining, destination.Length - destPos - _marker.Length);

            if (searchLength < _marker.Length) // Not enough space for another marker, copy the rest
            {
                source[srcPos..].CopyTo(destination[destPos..]);
                destPos += source.Length - srcPos;
                break;
            }

            int markerPos = source[srcPos..].IndexOf(_marker);

            if (markerPos == -1) // No more markers, copy the rest
            {
                source[srcPos..].CopyTo(destination[destPos..]);
                destPos += source.Length - srcPos;
                break;
            }

            int copyLength = markerPos + _marker.Length;
            source.Slice(srcPos, copyLength).CopyTo(destination[destPos..]);
            srcPos += copyLength;
            destPos += copyLength;
            destination[destPos] = EscapeMarker;
            destPos++;
        }

        return destPos;
    }
}
