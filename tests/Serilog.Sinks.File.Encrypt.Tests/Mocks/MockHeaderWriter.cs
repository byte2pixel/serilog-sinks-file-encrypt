using Serilog.Sinks.File.Encrypt.Interfaces;

namespace Serilog.Sinks.File.Encrypt.Tests.Mocks;

/// <summary>
/// Mock implementation of IHeaderWriter for testing purposes.
/// </summary>
public class MockHeaderWriter : IHeaderWriter
{
    public byte[] ExpectedHeader { get; private set; } = [];

    public ReadOnlySpan<byte> Encrypt(ReadOnlySpan<byte> aesKey, ReadOnlySpan<byte> nonce)
    {
        Span<byte> mockHeader = new byte[aesKey.Length + nonce.Length];
        aesKey.CopyTo(mockHeader);
        nonce.CopyTo(mockHeader[aesKey.Length..]);
        ExpectedHeader = mockHeader.ToArray();
        return mockHeader;
    }
}
