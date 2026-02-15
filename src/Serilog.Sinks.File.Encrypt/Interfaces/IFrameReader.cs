using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt.Interfaces;

/// <summary>
/// The <see cref="IFrameReader"/> interface defines the contract for reading encrypted log message frames from an input stream.
/// </summary>
public interface IFrameReader
{
    /// <summary>
    /// Reads the session header from the input stream, which includes the RSA-encrypted session key and nonce.
    /// </summary>
    /// <param name="input">The underlying stream.</param>
    /// <param name="keyMap">A dictionary mapping key identifiers to RSA instances for decryption.
    /// The reader will use the key identifier from the header to look up the appropriate RSA key for decrypting the session key.</param>
    /// <returns>A tuple containing the version, header data, and the total length of the session header (including version and header).</returns>
    public (
        byte version,
        RSA rsa,
        ReadOnlyMemory<byte> header,
        ReadOnlyMemory<byte> payload
    ) ReadHeader(Stream input, Dictionary<string, RSA> keyMap);
}
