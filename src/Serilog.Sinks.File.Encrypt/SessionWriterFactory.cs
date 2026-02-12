using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;
using Serilog.Sinks.File.Encrypt.Writers;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Factory for creating session writers based on the specified version.
/// </summary>
public static class SessionWriterFactory
{
    /// <summary>
    /// Creates a session writer for the specified version.
    /// </summary>
    /// <param name="options">The encryption options containing the version and public key information.</param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the specified version is not supported.
    /// </exception>
    public static ISessionWriter Create(EncryptionOptions options)
    {
        return options.Version switch
        {
            1 => new SessionWriterV1(new HeaderEncoderV1(options), new MessageEncryptorV1()),
            _ => throw new NotSupportedException(),
        };
    }
}
