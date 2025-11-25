using System.Security.Cryptography;
using System.Text;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Serilog.Sinks.File lifecycle hooks for log file encryption.
/// </summary>
public class EncryptHooks : FileLifecycleHooks
{
    private readonly RSA _rsaPublicKey;

    /// <summary>
    /// Creates a new instance of <see cref="EncryptHooks"/> with the provided RSA public key in XML format.
    /// </summary>
    /// <param name="rsaPublicKeyXml">The RSA public key in XML format.</param>
    public EncryptHooks(string rsaPublicKeyXml)
    {
        _rsaPublicKey = RSA.Create();
        _rsaPublicKey.FromXmlString(rsaPublicKeyXml);
    }

    /// <inheritdoc />
    public override Stream OnFileOpened(string path, Stream underlyingStream, Encoding encoding)
    {
        return new EncryptedStream(underlyingStream, _rsaPublicKey);
    }
}
