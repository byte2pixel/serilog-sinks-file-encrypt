using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Readers.v1;

/// <inheritdoc />
public class SessionReaderV1 : ISessionReader
{
    private readonly IHeaderDecryptor _headerDecryptor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionReaderV1"/> class with the specified header decryptor.
    /// </summary>
    /// <param name="headerDecryptor"></param>
    public SessionReaderV1(IHeaderDecryptor headerDecryptor)
    {
        _headerDecryptor = headerDecryptor;
    }

    /// <inheritdoc />
    public DecryptionContext ReadSession(RSA rsa, ReadOnlyMemory<byte> header)
    {
        // Decrypt the header to get the session key and nonce
        (byte[] aesKey, byte[] nonce, DateTimeOffset timestamp) = _headerDecryptor.Decrypt(
            rsa,
            header
        );

        return new DecryptionContext(EncryptionConstants.TagLength, nonce, aesKey);
    }
}
