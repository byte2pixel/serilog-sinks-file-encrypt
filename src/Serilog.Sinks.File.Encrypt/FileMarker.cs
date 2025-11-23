namespace Serilog.Sinks.File.Encrypt;

internal static class FileMarker
{
    // csharpier-ignore-start
    internal static readonly byte[] LogHeadMarker = [0xFF, 0xFE, 0x4C, 0x4F, 0x47, 0x48, 0x44, 0x00, 0x01];
    internal static readonly byte[] LogBodyMarker = [0xFF, 0xFE, 0x4C, 0x4F, 0x47, 0x42, 0x44, 0x00, 0x02];
    // csharpier-ignore-end
    
    internal static readonly int MarkerLength = LogHeadMarker.Length;
    private const int IntSize = 4;
    private const int MinKeyIvLength = 256;
    private const int MaxKeyIvLength = 4096;

    /// <summary>
    /// Validates that a potential header marker is followed by reasonable key/IV length values
    /// Stream position will be restored to the original position after validation.
    /// </summary>
    public static bool IsValidHeaderMarker(FileStream stream, long markerPosition)
    {
        long originalPosition = stream.Position;
        try
        {
            stream.Position = markerPosition + MarkerLength;

            byte[] keyLengthBytes = new byte[IntSize];
            byte[] ivLengthBytes = new byte[IntSize];

            if (stream.Read(keyLengthBytes) != IntSize || stream.Read(ivLengthBytes) != IntSize)
                return false;

            int keyLength = BitConverter.ToInt32(keyLengthBytes, 0);
            int ivLength = BitConverter.ToInt32(ivLengthBytes, 0);

            // Validate the lengths are reasonable for RSA encrypted AES keys/IVs
            return keyLength is >= MinKeyIvLength and <= MaxKeyIvLength && ivLength is >= MinKeyIvLength and <= MaxKeyIvLength;
        }
        catch
        {
            return false;
        }
        finally
        {
            // Restore original position so calling code can read the header data
            stream.Position = originalPosition;
        }
    }
}
