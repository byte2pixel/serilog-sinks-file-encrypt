using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Readers;

namespace Serilog.Sinks.File.Encrypt;

internal static class MessageDecryptorFactory
{
    public static IMessageDecryptor GetMessageDecryptor(byte version)
    {
        return version switch
        {
            1 => new MessageDecryptorV1(),
            _ => throw new NotSupportedException($"Unsupported encryption version: {version}"),
        };
    }
}
