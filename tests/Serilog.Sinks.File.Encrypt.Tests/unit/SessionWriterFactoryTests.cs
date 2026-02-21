using Serilog.Sinks.File.Encrypt.Interfaces;
using Serilog.Sinks.File.Encrypt.Models;
using Serilog.Sinks.File.Encrypt.Writers.v1;

namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class SessionWriterFactoryTests
{
    [Fact]
    public void GivenSupportedVersion_WhenCreate_ThenSessionWriterIsReturned()
    {
        // Arrange
        using RSA rsa = RSA.Create();
        ISessionWriter writer = SessionWriterFactory.Create(TestUtils.GetEncryptionOptions(rsa));
        writer.ShouldBeOfType<SessionWriterV1>();
    }

    [Fact]
    public void GivenUnsupportedVersion_WhenCreate_ThenNotSupportedExceptionIsThrown()
    {
        using RSA rsa = RSA.Create();
        // Arrange
        EncryptionOptions options = TestUtils.GetEncryptionOptions(rsa, version: 999);

        // Act & Assert
        NotSupportedException exception = Should.Throw<NotSupportedException>(() =>
            SessionWriterFactory.Create(options)
        );
        exception.Message.ShouldBe("Version 999 is not supported.");
    }
}
