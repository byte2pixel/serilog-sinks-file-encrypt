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

        using var ms = new MemoryStream();

        // Act
        ReadOnlySpan<byte> keyIdBytes = Guid.NewGuid().ToByteArray().AsSpan();
        frameWriter.WriteHeader(ms, Version, keyIdBytes, header);

        // Assert
        byte[] expectedOutput = EncryptionConstants
            .MagicBytes.Concat([Version])
            .Concat(keyIdBytes.ToArray())
            .Concat(header)
            .ToArray();

        Assert.Equal(expectedOutput, ms.ToArray());
    }
}
