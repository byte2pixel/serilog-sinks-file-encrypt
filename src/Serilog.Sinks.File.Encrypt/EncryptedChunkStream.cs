using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt;

internal class EncryptedChunkStream : Stream
{
    private const long MaxChunkSize = 10 * 1024 * 1024; // 10 MB chunks
    private readonly Stream _underlyingStream;
    private readonly RSA _rsaPublicKey;
    
    private CryptoStream? _currentCryptoStream;
    private byte[]? _currentKey;
    private byte[]? _currentIv;
    private MemoryStream _bufferStream;
    private long _currentChunkSize;
    
    private bool _isDisposed;
    
    public EncryptedChunkStream(Stream underlyingStream, RSA rsaPublicKey)
    {
        _underlyingStream = underlyingStream;
        _rsaPublicKey = rsaPublicKey;
        _bufferStream = new MemoryStream();

        // Start a new encryption chunk immediately
        StartNewEncryptionChunk();
    }

    private void StartNewEncryptionChunk()
    {
        // Generate a new random key and IV for this chunk
        using Aes aes = Aes.Create();
        _currentKey = aes.Key;
        _currentIv = aes.IV;

        // Write chunk marker and encrypted keys
        byte[] encryptedKey = _rsaPublicKey.Encrypt(_currentKey, RSAEncryptionPadding.OaepSHA256);
        byte[] encryptedIv = _rsaPublicKey.Encrypt(_currentIv, RSAEncryptionPadding.OaepSHA256);

        // Write chunk header
        byte[] chunkMarker = "LOGCHUNK"u8.ToArray();
        _underlyingStream.Write(chunkMarker, 0, chunkMarker.Length);
        _underlyingStream.Write(BitConverter.GetBytes(encryptedKey.Length), 0, sizeof(int));
        _underlyingStream.Write(BitConverter.GetBytes(encryptedIv.Length), 0, sizeof(int));
        _underlyingStream.Write(encryptedKey, 0, encryptedKey.Length);
        _underlyingStream.Write(encryptedIv, 0, encryptedIv.Length);

        // Create a new crypto stream - store the Aes instance to ensure it's not disposed
        Aes aesAlg = Aes.Create();
        aesAlg.Padding = PaddingMode.PKCS7; // Ensure proper padding
        _currentCryptoStream?.Dispose();
        _currentCryptoStream = new CryptoStream(
            _underlyingStream,
            aesAlg.CreateEncryptor(_currentKey, _currentIv),
            CryptoStreamMode.Write,
            leaveOpen: true
        );
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _bufferStream.Write(buffer, offset, count);
    }

    public override void Flush()
    {
        if (_bufferStream.Length <= 0) return;
        byte[] data = _bufferStream.ToArray();

        // Check if we should rotate to a new chunk
        if (_currentChunkSize + data.Length > MaxChunkSize)
        {
            RotateChunk();
        }

        _currentCryptoStream?.Write(data, 0, data.Length);
        _currentChunkSize += data.Length;

        _bufferStream = new MemoryStream();
    }
    
    private void RotateChunk()
    {
        // Finalize current chunk
        if (_currentCryptoStream != null)
        {
            _currentCryptoStream.FlushFinalBlock();
            _currentCryptoStream.Dispose();
        }

        // Start new chunk
        _currentChunkSize = 0;
        StartNewEncryptionChunk();
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed) return;
        
        _isDisposed = true;
        
        if (disposing)
        {
            // Flush any remaining data
            Flush();

            if (_currentCryptoStream != null)
            {
                // Important: Finalize the crypto stream properly
                _currentCryptoStream.FlushFinalBlock();
                _currentCryptoStream.Dispose();
            }
            _bufferStream.Dispose();
            _underlyingStream.Dispose();
        }
        base.Dispose(disposing);
    }

    // Required Stream implementations
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
