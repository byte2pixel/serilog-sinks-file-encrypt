namespace Serilog.Sinks.File.Encrypt.Tests;

public class FrameWriterTests
{
    [Fact]
    public void FrameWriter_Composes_Header_Correctly()
    {
        // Arrange
        var frameWriter = new FrameWriter();
        const byte Version = 2;
        byte[] header = [1, 2, 3, 4];

        // Act
        ReadOnlySpan<byte> keyIdBytes = Guid.NewGuid().ToByteArray().AsSpan();
        Span<byte> destination =
            stackalloc byte[
                CryptographicUtils.MagicBytes.Length + 1 + keyIdBytes.Length + header.Length
            ];
        int written = frameWriter.WriteHeader(destination, Version, keyIdBytes, header);

        // Assert
        byte[] expectedOutput = CryptographicUtils
            .MagicBytes.Concat([Version])
            .Concat(keyIdBytes.ToArray())
            .Concat(header)
            .ToArray();

        written.ShouldBe(expectedOutput.Length);
        destination[..written].ToArray().ShouldBe(expectedOutput);
    }
}
