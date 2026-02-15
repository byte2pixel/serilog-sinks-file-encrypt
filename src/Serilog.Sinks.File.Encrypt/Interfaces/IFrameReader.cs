using System.Security.Cryptography;

namespace Serilog.Sinks.File.Encrypt.Interfaces;

/// <summary>
/// The <see cref="IFrameReader"/> interface defines the contract for reading encrypted log session headers from an input stream.
/// </summary>
public interface IFrameReader
{
    /// <summary>
    /// Reads the session header from the input stream, which includes the RSA-encrypted session key and nonce.
    /// Messages following the header are self-framing with length prefixes and should be read separately.
    /// </summary>
    /// <param name="input">The underlying stream.</param>
    /// <param name="keyMap">A dictionary mapping key identifiers to RSA instances for decryption.
    /// The reader will use the key identifier from the header to look up the appropriate RSA key for decrypting the session key.</param>
    /// <returns>A tuple containing the version, RSA key, and RSA-encrypted header data.</returns>
    (byte version, RSA rsa, ReadOnlyMemory<byte> header) ReadHeader(
        Stream input,
        Dictionary<string, RSA> keyMap
    );

    /// <summary>
    /// Attempts to resync to the next valid session header after encountering corruption.
    /// Scans for magic bytes, then validates by attempting RSA decryption.
    /// </summary>
    /// <param name="input">The input stream to scan.</param>
    /// <param name="keyMap">A dictionary mapping key identifiers to RSA instances for validation.</param>
    /// <param name="position">When successful, the stream position of the start of the valid session header.</param>
    /// <returns>True if a valid session header was found; false if EOF was reached.</returns>
    bool TryResyncToNextSession(Stream input, Dictionary<string, RSA> keyMap, out long position);
}
