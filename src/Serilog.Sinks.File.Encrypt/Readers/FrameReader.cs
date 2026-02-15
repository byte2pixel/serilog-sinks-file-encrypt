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
    public (byte version, RSA rsa, ReadOnlyMemory<byte> header) ReadHeader(
        Stream input,
        Dictionary<string, RSA> keyMap
    )
    {
        // Read the version byte (1 byte)
        Span<byte> version = stackalloc byte[HeaderMetadata.Version];
        input.ReadExactly(version);

        HeaderMetadata metadata = HeaderMetadataFactory.Create(version[0]);

        Span<byte> keyId = new byte[metadata.KeyIdLength];

        // Read the key ID bytes
        int bytesRead = input.Read(keyId);
        if (bytesRead != metadata.KeyIdLength)
        {
            throw new EndOfStreamException(
                $"Unexpected end of stream while reading key ID. Expected {metadata.KeyIdLength} bytes, but only read {bytesRead} bytes."
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

        // Compute the header size based on the RSA key size / 8
        int headerSize = rsa.KeySize / 8;

        // Read the RSA-encrypted header
        Span<byte> header = new byte[headerSize];
        input.ReadExactly(header);

        return (version[0], rsa, header.ToArray());
    }

    /// <inheritdoc />
    public bool TryResyncToNextSession(
        Stream input,
        Dictionary<string, RSA> keyMap,
        out long position
    )
    {
        position = -1;

        if (!input.CanSeek)
        {
            return false;
        }

        byte[] magicBytes = EncryptionConstants.MagicBytes;
        int magicLength = magicBytes.Length;

        while (true)
        {
            long scanPosition = ScanForMagicBytes(input, magicBytes);
            if (scanPosition == -1)
            {
                return false; // No more magic bytes found, EOF reached
            }

            // Position stream after magic bytes
            input.Seek(scanPosition + magicLength, SeekOrigin.Begin);

            var result = TryValidateSessionAtPosition(input, keyMap);
            switch (result)
            {
                case SessionValidationResult.Valid:
                    position = scanPosition;
                    input.Seek(scanPosition, SeekOrigin.Begin);
                    return true;
                case SessionValidationResult.EndOfStream:
                    return false;
                default:
                    input.Seek(scanPosition + 1, SeekOrigin.Begin);
                    continue;
            }
        }
    }

    private enum SessionValidationResult
    {
        Valid,
        Invalid,
        EndOfStream,
    }

    private static SessionValidationResult TryValidateSessionAtPosition(
        Stream input,
        Dictionary<string, RSA> keyMap
    )
    {
        try
        {
            // Try to read version
            int versionByte = input.ReadByte();
            if (versionByte == -1)
            {
                return SessionValidationResult.EndOfStream;
            }

            // Check if version is supported
            if (versionByte != 1)
            {
                return SessionValidationResult.Invalid;
            }

            HeaderMetadata metadata = HeaderMetadataFactory.Create((byte)versionByte);

            // Read keyId
            byte[] keyIdBuffer = new byte[metadata.KeyIdLength];
            int bytesRead = input.Read(keyIdBuffer, 0, metadata.KeyIdLength);
            if (bytesRead != metadata.KeyIdLength)
            {
                return SessionValidationResult.EndOfStream;
            }

            string keyIdStr = System.Text.Encoding.UTF8.GetString(keyIdBuffer).TrimEnd('\0');
            if (!keyMap.TryGetValue(keyIdStr, out RSA? rsa))
            {
                return SessionValidationResult.Invalid;
            }

            // Try to read and decrypt RSA payload
            int headerSize = rsa.KeySize / 8;
            byte[] rsaPayload = new byte[headerSize];
            bytesRead = input.Read(rsaPayload, 0, headerSize);
            if (bytesRead != headerSize)
            {
                return SessionValidationResult.EndOfStream;
            }

            // Attempt RSA decryption - this validates the session header
            _ = rsa.Decrypt(rsaPayload, RSAEncryptionPadding.OaepSHA256);

            return SessionValidationResult.Valid;
        }
        catch (CryptographicException)
        {
            return SessionValidationResult.Invalid;
        }
        catch (EndOfStreamException)
        {
            return SessionValidationResult.EndOfStream;
        }
    }

    /// <summary>
    /// Scans the input stream for the magic bytes sequence.
    /// </summary>
    /// <param name="input">The stream to scan.</param>
    /// <param name="magicBytes">The magic bytes to search for.</param>
    /// <returns>The position of the first byte of magic sequence, or -1 if not found.</returns>
    private static long ScanForMagicBytes(Stream input, byte[] magicBytes)
    {
        int magicLength = magicBytes.Length;
        int matchIndex = 0;

        while (true)
        {
            int b = input.ReadByte();
            if (b == -1)
            {
                return -1; // EOF
            }

            if (b == magicBytes[matchIndex])
            {
                matchIndex++;
                if (matchIndex == magicLength)
                {
                    // Found complete magic sequence
                    return input.Position - magicLength;
                }
            }
            else if (matchIndex > 0)
            {
                // Partial match failed, backtrack
                // Move back to check if the current byte could start a new match
                matchIndex = (b == magicBytes[0]) ? 1 : 0;
            }
        }
    }
}
