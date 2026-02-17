using Serilog.Sinks.File.Encrypt.Interfaces;

namespace Serilog.Sinks.File.Encrypt.Tests.unit.v1;

/// <summary>
/// Mock implementation of IHeaderEncryptor for testing purposes. This class simulates the behavior of the header encryptor used in version 1 of the encrypted log format.
/// </summary>
public class MockHeaderEncryptorV1 : IHeaderEncryptor
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
