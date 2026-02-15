using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Readers;

/// <summary>
/// The <see cref="FrameReader"/> class implements the <see cref="IFrameReader"/> interface and provides functionality to read encrypted log session headers from an input stream.
/// </summary>
public class FrameReader : IFrameReader
{
    /// <inheritdoc />
    public async Task<(byte version, RSA rsa, ReadOnlyMemory<byte> header)> ReadHeaderAsync(
        Stream input,
        Dictionary<string, RSA> keyMap,
        CancellationToken cancellationToken
    )
    {
        // Read the version byte (1 byte)
        byte version = (byte)input.ReadByte();

        HeaderMetadata metadata = HeaderMetadataFactory.Create(version);

        Memory<byte> keyId = new byte[metadata.KeyIdLength];

        // Read the key ID bytes
        int bytesRead = await input.ReadAsync(keyId, cancellationToken);
        if (bytesRead != metadata.KeyIdLength)
        {
            throw new EndOfStreamException(
                $"Unexpected end of stream while reading key ID. Expected {metadata.KeyIdLength} bytes, but only read {bytesRead} bytes."
            );
        }

        // Validate that the key ID exists in the provided key map
        string keyIdStr = System.Text.Encoding.UTF8.GetString(keyId.Span).TrimEnd('\0');
        if (!keyMap.TryGetValue(keyIdStr, out RSA? rsa))
        {
            throw new CryptographicException(
                $"No RSA private key found for KeyId: '{keyIdStr}'. Available keys: {string.Join(", ", keyMap.Keys.Select(k => $"'{k}'"))}"
            );
        }

        // Compute the header size based on the RSA key size / 8
        int headerSize = rsa.KeySize / 8;

        // Read the RSA-encrypted header
        Span<byte> header = new byte[headerSize];
        input.ReadExactly(header);

        return (version, rsa, header.ToArray());
    }
}
