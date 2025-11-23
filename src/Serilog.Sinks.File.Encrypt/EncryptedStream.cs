using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt;

public class EncryptedStream : Stream
{
    private static readonly byte[] HeaderMarker = { 0xFF, 0xFE, 0x4C, 0x4F, 0x47, 0x48, 0x44, 0x00, 0x01 }; // LOGHD with validation
    private static readonly byte[] ChunkMarker = { 0xFF, 0xFE, 0x4C, 0x4F, 0x47, 0x42, 0x44, 0x00, 0x02 }; // LOGBD with validation
    private readonly Stream _underlyingStream;
    private readonly ICryptoTransform _currentCryptoTransform;
    private readonly Aes _aes;

    private bool _isDisposed;
    private bool _needsNewChunk = true;
    private MemoryStream? _bufferStream;
    private CryptoStream? _currentCryptoStream;

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

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

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

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

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
