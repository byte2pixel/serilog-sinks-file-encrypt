using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;
using Serilog.Sinks.File.Encrypt.Writers.v1;

namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Factory for creating message encryptors based on the specified version.
/// </summary>
internal static class MessageEncryptorFactory
{
    /// <summary>
    /// Creates a message encryptor for the specified version.
    /// </summary>
    /// <param name="options">The encryption options containing the version information.</param>
    /// <returns>A message encryptor for the specified version.</returns>
    /// <exception cref="NotSupportedException">Thrown when the specified version is not supported.</exception>
    internal static IMessageEncryptor Create(EncryptionOptions options)
    {
        return options.Version switch
        {
            1 => new MessageEncryptorV1(),
            _ => throw new NotSupportedException($"Version {options.Version} is not supported."),
        };
    }
}
