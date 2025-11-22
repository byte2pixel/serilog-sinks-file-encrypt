using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt;

public class EncryptedStream : Stream
{
    private static readonly byte[] ChunkMarker = "LOGCHUNK"u8.ToArray();
    private readonly Stream _underlyingStream;
    private readonly ICryptoTransform _currentCryptoTransform;
    private readonly Aes _aes;
    private readonly byte[] _encryptedKey;
    private readonly byte[] _encryptedKeyLength;
    private readonly byte[] _encryptedIv;
    private readonly byte[] _encryptedIvLength;
    
    private bool _isDisposed;
    private bool _needsNewChunk = false;
    private CryptoStream? _currentCryptoStream;

    public EncryptedStream(Stream underlyingStream, RSA rsaPublicKey)
    {
        _underlyingStream = underlyingStream;
        
        // Generate a new random key and IV for this stream
        _aes = Aes.Create();
        byte[] currentKey = _aes.Key;
        byte[] currentIv = _aes.IV;
        _aes.Padding = PaddingMode.PKCS7; // Ensure proper padding
        _currentCryptoTransform = _aes.CreateEncryptor(currentKey, currentIv);
        
        // Write chunk marker and encrypted keys
        _encryptedKey = rsaPublicKey.Encrypt(currentKey, RSAEncryptionPadding.OaepSHA256);
        _encryptedKeyLength = BitConverter.GetBytes(_encryptedKey.Length);
        _encryptedIv = rsaPublicKey.Encrypt(currentIv, RSAEncryptionPadding.OaepSHA256);
        _encryptedIvLength = BitConverter.GetBytes(_encryptedIv.Length);
        
        StartNewEncryptionChunk();
    }

    private void StartNewEncryptionChunk()
    {
        // Write chunk header
        _underlyingStream.Write(ChunkMarker, 0, ChunkMarker.Length);
        _underlyingStream.Write(_encryptedKeyLength, 0, sizeof(int));
        _underlyingStream.Write(_encryptedIvLength, 0, sizeof(int));
        _underlyingStream.Write(_encryptedKey, 0, _encryptedKey.Length);
        _underlyingStream.Write(_encryptedIv, 0, _encryptedIv.Length);

        // Create a new crypto stream - store the Aes instance to ensure it's not disposed
        _currentCryptoStream?.Dispose();
        _currentCryptoStream = new CryptoStream(
            _underlyingStream,
            _currentCryptoTransform,
            CryptoStreamMode.Write,
            leaveOpen: true
        );
    }
    
    public override void Flush()
    {
        if (_currentCryptoStream != null)
        {
            _currentCryptoStream.FlushFinalBlock();
            _currentCryptoStream.Dispose();
            _currentCryptoStream = null;
            _needsNewChunk = true;
        }
        _underlyingStream.Flush();
    }
    
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_currentCryptoStream != null)
        {
            await _currentCryptoStream.FlushFinalBlockAsync(cancellationToken);
            await _currentCryptoStream.DisposeAsync();
            _currentCryptoStream = null;
            _needsNewChunk = true;
        }
        await _underlyingStream.FlushAsync(cancellationToken);
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
    
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        
        if (_needsNewChunk || _currentCryptoStream == null)
        {
            StartNewEncryptionChunk();
            _needsNewChunk = false;
        }
        if (_currentCryptoStream != null)
        {
            await _currentCryptoStream.WriteAsync(buffer, cancellationToken);
        }
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    
    
    protected override void Dispose(bool disposing)
    {
        if (_isDisposed) return;
        
        _isDisposed = true;
        
        if (disposing)
        {
            if (_currentCryptoStream != null)
            {
                // Important: Finalize the crypto stream properly
                _currentCryptoStream.FlushFinalBlock();
                _currentCryptoStream.Dispose();
            }
            _underlyingStream.Dispose();
            _currentCryptoTransform.Dispose();
            _aes.Dispose();
        }
        base.Dispose(disposing);
    }
}