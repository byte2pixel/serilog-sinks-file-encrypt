using System.Buffers.Binary;
using System.Security.Cryptography;
using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Readers;

/// <summary>
/// The <see cref="FrameReader"/> class implements the <see cref="IFrameReader"/> interface and provides functionality to read encrypted log message frames from an input stream.
/// </summary>
public class FrameReader : IFrameReader
{
    /// <inheritdoc />
    public (
        byte version,
        RSA rsa,
        ReadOnlyMemory<byte> header,
        ReadOnlyMemory<byte> payload
    ) ReadHeader(Stream input, Dictionary<string, RSA> keyMap)
    {
        // Read the version byte (1 byte)
        Span<byte> version = stackalloc byte[HeaderMetadata.Version];
        input.ReadExactly(version);

        HeaderMetadata metadata = HeaderMetadataFactory.Create(version[0]);

        Span<byte> keyId = new byte[metadata.KeyIdLength];

        // read the key ID bytes
        int bytesRead = input.Read(keyId);
        if (bytesRead != metadata.KeyIdLength)
        {
            throw new EndOfStreamException(
                $"Unexpected end of stream while reading key ID. Expected 32 bytes, but only read {bytesRead} bytes."
            );
        }

        // Validate that the key ID exists in the provided key map
        string keyIdStr = System.Text.Encoding.UTF8.GetString(keyId).TrimEnd('\0');
        if (!keyMap.TryGetValue(keyIdStr, out RSA? rsa))
        {
            throw new CryptographicException(
                $"No RSA private key found for KeyId: '{keyIdStr}'. Available keys: {string.Join(", ", keyMap.Keys.Select(k => $"'{k}'"))}"
            );
        }

        Span<byte> lengthBuffer = stackalloc byte[HeaderMetadata.SessionLength];
        bytesRead = input.Read(lengthBuffer);
        if (bytesRead != HeaderMetadata.SessionLength)
        {
            throw new EndOfStreamException("Unexpected end of stream while reading header length.");
        }

        int sessionLength = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);

        // Compute the header size based on the RSA key size / 8
        int headerSize = rsa.KeySize / 8;

        // Read the header
        Span<byte> header = new byte[headerSize];
        input.ReadExactly(header);

        // Read the payload
        int payloadLength = sessionLength - headerSize;
        if (payloadLength < 0)
        {
            throw new InvalidDataException(
                $"Invalid session length. Calculated payload length is negative: {payloadLength}. Check the session length and header size."
            );
        }
        Span<byte> payload = new byte[payloadLength];
        input.ReadExactly(payload);

        return (version[0], rsa, header.ToArray(), payload.ToArray());
    }
}
