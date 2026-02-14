using Serilog.Sinks.File.Encrypt.Models;
using Serilog.Sinks.File.Encrypt.Writers;

namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class FrameWriterTests
{
    [Fact]
    public void FrameWriter_Writes_Header_Correctly()
    {
        // Arrange
        var frameWriter = new FrameWriter();
        const byte Version = 1;
        byte[] header = [1, 2, 3, 4];
        int sessionLength = header.Length + 16; // Example session length
        using var ms = new MemoryStream();

        // Act
        frameWriter.WriteHeader(ms, Version, header, sessionLength);

        // Assert
        byte[] expectedMagicBytes = [0x00, 0x42, 0x32, 0x50, 0xFF, 0xDA, 0x7E, 0x00];
        byte[] expectedSessionLengthBytes = BitConverter.GetBytes(sessionLength);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(expectedSessionLengthBytes);
        }

        byte[] expectedOutput = expectedMagicBytes
            .Concat([Version])
            .Concat(expectedSessionLengthBytes)
            .Concat(header)
            .ToArray();

        Assert.Equal(expectedOutput, ms.ToArray());
    }

    [Fact]
    public void FrameWriter_Writes_Message_Correctly()
    {
        // Arrange
        var frameWriter = new FrameWriter();
        var encryptedMessage = new EncryptedMessage
        {
            Ciphertext = [5, 6, 7, 8],
            Tag = [9, 10, 11, 12],
        };
        using var ms = new MemoryStream();

        // Act
        frameWriter.WriteMessage(ms, encryptedMessage);

        // Assert
        byte[] expectedMessageLengthBytes = BitConverter.GetBytes(encryptedMessage.TotalLength);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(expectedMessageLengthBytes);
        }

        byte[] expectedOutput = expectedMessageLengthBytes
            .Concat(encryptedMessage.Ciphertext)
            .Concat(encryptedMessage.Tag)
            .ToArray();

        Assert.Equal(expectedOutput, ms.ToArray());
    }
}
