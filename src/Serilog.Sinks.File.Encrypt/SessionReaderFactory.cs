using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Readers.v1;

namespace Serilog.Sinks.File.Encrypt;

internal static class SessionReaderFactory
{
    public static ISessionReader GetSessionReader(int version)
    {
        return version switch
        {
            1 => new SessionReaderV1(new HeaderDecryptorV1()),
            _ => throw new NotSupportedException($"Unsupported encryption version: {version}"),
        };
    }

    public static ISessionReader GetSessionReader(byte version)
    {
        return version switch
        {
            1 => new SessionReaderV1(new HeaderDecryptorV1()),
            _ => throw new NotSupportedException($"Unsupported encryption version: {version}"),
        };
    }
}
