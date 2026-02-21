using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Readers.v1;

namespace Serilog.Sinks.File.Encrypt;

internal static class SessionReaderFactory
{
    /// <summary>
    /// Get the appropriate session reader based on the version number read from the log file header.
    /// </summary>
    /// <param name="version">The byte that contains the version number for the session header</param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException">Unsupported version</exception>
    internal static ISessionReader GetSessionReader(byte version)
    {
        return version switch
        {
            1 => new SessionReaderV1(),
            _ => throw new NotSupportedException($"Unsupported encryption version: {version}"),
        };
    }
}
