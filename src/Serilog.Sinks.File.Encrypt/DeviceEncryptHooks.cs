using System.Security.Cryptography;
using System.Text;

namespace Serilog.Sinks.File.Encrypt;

public class DeviceEncryptHooks : FileLifecycleHooks
{
    private readonly RSA _rsaPublicKey;

    public DeviceEncryptHooks(string rsaPublicKeyXml)
    {
        _rsaPublicKey = RSA.Create();
        _rsaPublicKey.FromXmlString(rsaPublicKeyXml);
    }

    public override Stream OnFileOpened(string path, Stream underlyingStream, Encoding encoding)
    {
        return new EncryptedStream(underlyingStream, _rsaPublicKey);
    }
}
