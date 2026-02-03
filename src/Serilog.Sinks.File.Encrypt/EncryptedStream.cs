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
    private static readonly byte _escapeMarker = 0x00;
    // csharpier-ignore-end

    // length of the nonce in bytes (counter for the stream cypher)
    private const int NONCE_LENGTH = 12;
    // length of the AES session key in bytes
    private const int SESSION_KEY_LENGTH = 32; // 256 bit
    // tag (hmac) to confirm data integrity
    private const int TAG_LENGTH = 12; // 96 bit

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
        byte[] sessionKey = new byte[SESSION_KEY_LENGTH];
        _nonce = new byte[NONCE_LENGTH];

        RandomNumberGenerator.Fill(sessionKey);
        RandomNumberGenerator.Fill(_nonce);

        _aes = new AesGcm(sessionKey, TAG_LENGTH);

        byte[] encryptedTagSize = _rsaPublicKey.Encrypt(BitConverter.GetBytes(TAG_LENGTH), RSAEncryptionPadding.OaepSHA256);
        byte[] encryptedNonce = _rsaPublicKey.Encrypt(_nonce, RSAEncryptionPadding.OaepSHA256);
        byte[] encryptedSessionKey = _rsaPublicKey.Encrypt(sessionKey, RSAEncryptionPadding.OaepSHA256);

        byte[] encryptedTagSizeLength = BitConverter.GetBytes(encryptedTagSize.Length);
        byte[] encryptedNonceLength = BitConverter.GetBytes(encryptedNonce.Length);
        byte[] encryptedSessionKeyLength = BitConverter.GetBytes(encryptedSessionKey.Length);

        byte[] messageHeader = [
            ..encryptedTagSize,
            ..encryptedNonce,
            ..encryptedSessionKey];

        messageHeader.Escape(_marker, _escapeMarker);

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
        await _underlyingStream.FlushAsync(cancellationToken)
            .ConfigureAwait(false);
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

        await _underlyingStream.WriteAsync(
            Transform(buffer.Span),
            cancellationToken
        ).ConfigureAwait(false);
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
            }

            _underlyingStream.Dispose();
        }

        base.Dispose(disposing);
    }

    private byte[] Transform(ReadOnlySpan<byte> plainText)
    {
        byte[] cypherText = new byte[plainText.Length];
        byte[] hmac = new byte[TAG_LENGTH];

        _aes!.Encrypt(_nonce, plainText, cypherText, hmac);

        // increase the nonce for next block
        _nonce.IncreaseNonce();

        // concat and escape message data
        byte[] message = [.. cypherText, .. hmac];
        message.Escape(_marker, _escapeMarker);

        return [.. BitConverter.GetBytes(message.Length), .. message];
    }
}
