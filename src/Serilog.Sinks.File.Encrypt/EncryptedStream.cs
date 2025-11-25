using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Encrypts data written to the underlying stream using AES encryption.
/// </summary>
public class EncryptedStream : Stream
{
    // csharpier-ignore-start
    private static readonly byte[] HeaderMarker = [ 0xFF, 0xFE, 0x4C, 0x4F, 0x47, 0x48, 0x44, 0x00, 0x01 ];
    private static readonly byte[] ChunkMarker =  [ 0xFF, 0xFE, 0x4C, 0x4F, 0x47, 0x42, 0x44, 0x00, 0x02 ];
    // csharpier-ignore-end

    private readonly Stream _underlyingStream;
    private readonly ICryptoTransform _currentCryptoTransform;
    private readonly Aes _aes;

    private bool _isDisposed;
    private bool _needsNewChunk = true;
    private MemoryStream? _bufferStream;
    private CryptoStream? _currentCryptoStream;

    /// <summary>
    /// Creates a new instance of <see cref="EncryptedStream"/> that encrypts data written to the underlying stream.
    /// </summary>
    /// <param name="underlyingStream">The stream to write encrypted data to.</param>
    /// <param name="rsaPublicKey">The RSA public key used to encrypt the AES key and IV.</param>
    public EncryptedStream(Stream underlyingStream, RSA rsaPublicKey)
    {
        _underlyingStream = underlyingStream;

        // Generate a new random key and IV for this stream
        _aes = Aes.Create();
        byte[] currentKey = _aes.Key;
        byte[] currentIv = _aes.IV;
        _aes.Padding = PaddingMode.PKCS7;
        _currentCryptoTransform = _aes.CreateEncryptor(currentKey, currentIv);

        byte[] encryptedKey = rsaPublicKey.Encrypt(currentKey, RSAEncryptionPadding.OaepSHA256);
        byte[] encryptedKeyLength = BitConverter.GetBytes(encryptedKey.Length);
        byte[] encryptedIv = rsaPublicKey.Encrypt(currentIv, RSAEncryptionPadding.OaepSHA256);
        byte[] encryptedIvLength = BitConverter.GetBytes(encryptedIv.Length);
        _underlyingStream.Write(HeaderMarker, 0, HeaderMarker.Length);
        _underlyingStream.Write(encryptedKeyLength, 0, sizeof(int));
        _underlyingStream.Write(encryptedIvLength, 0, sizeof(int));
        _underlyingStream.Write(encryptedKey, 0, encryptedKey.Length);
        _underlyingStream.Write(encryptedIv, 0, encryptedIv.Length);
    }

    private void StartNewEncryptionChunk()
    {
        // Create a memory buffer to hold the encrypted content
        _bufferStream = new MemoryStream();
        _currentCryptoStream = new CryptoStream(
            _bufferStream,
            _currentCryptoTransform,
            CryptoStreamMode.Write,
            leaveOpen: true
        );
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        if (_currentCryptoStream != null && _bufferStream != null)
        {
            // Finalize the encryption
            _currentCryptoStream.FlushFinalBlock();
            _currentCryptoStream.Dispose();
            _currentCryptoStream = null;

            // Get the encrypted data from the buffer
            byte[] encryptedData = _bufferStream.ToArray();
            _bufferStream.Dispose();
            _bufferStream = null;

            // Write the complete chunk: [MARKER][LENGTH][ENCRYPTED_DATA]
            _underlyingStream.Write(ChunkMarker, 0, ChunkMarker.Length);
            byte[] lengthBytes = BitConverter.GetBytes(encryptedData.Length);
            _underlyingStream.Write(lengthBytes, 0, sizeof(int));
            _underlyingStream.Write(encryptedData, 0, encryptedData.Length);

            _needsNewChunk = true;
        }
        _underlyingStream.Flush();
    }

    /// <inheritdoc/>
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_currentCryptoStream != null && _bufferStream != null)
        {
            // Finalize the encryption
            await _currentCryptoStream
                .FlushFinalBlockAsync(cancellationToken)
                .ConfigureAwait(false);
            await _currentCryptoStream.DisposeAsync().ConfigureAwait(false);
            _currentCryptoStream = null;

            // Get the encrypted data from the buffer
            byte[] encryptedData = _bufferStream.ToArray();
            await _bufferStream.DisposeAsync().ConfigureAwait(false);
            _bufferStream = null;

            // Write the complete chunk: [MARKER][LENGTH][ENCRYPTED_DATA]
            await _underlyingStream
                .WriteAsync(ChunkMarker, cancellationToken)
                .ConfigureAwait(false);
            byte[] lengthBytes = BitConverter.GetBytes(encryptedData.Length);
            await _underlyingStream
                .WriteAsync(lengthBytes.AsMemory(0, sizeof(int)), cancellationToken)
                .ConfigureAwait(false);
            await _underlyingStream
                .WriteAsync(encryptedData, cancellationToken)
                .ConfigureAwait(false);

            _needsNewChunk = true;
        }
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
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_needsNewChunk || _currentCryptoStream == null)
        {
            StartNewEncryptionChunk();
            _needsNewChunk = false;
        }
        _currentCryptoStream?.Write(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_needsNewChunk || _currentCryptoStream == null)
        {
            StartNewEncryptionChunk();
            _needsNewChunk = false;
        }
        _currentCryptoStream?.Write(buffer);
    }

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_needsNewChunk || _currentCryptoStream == null)
        {
            StartNewEncryptionChunk();
            _needsNewChunk = false;
        }

        if (_currentCryptoStream != null)
        {
            await _currentCryptoStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
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
            return;

        _isDisposed = true;

        if (disposing)
        {
            // Flush any remaining data before disposing
            if (_currentCryptoStream != null || _bufferStream != null)
            {
                try
                {
                    Flush();
                }
                catch
                {
                    // Ignore errors during disposal
                }
            }

            _currentCryptoStream?.Dispose();
            _bufferStream?.Dispose();
            _underlyingStream.Dispose();
            _currentCryptoTransform.Dispose();
            _aes.Dispose();
        }
        base.Dispose(disposing);
    }
}
