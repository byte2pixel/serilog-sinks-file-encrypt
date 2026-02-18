using Serilog.Sinks.File.Encrypt.Models;

namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class LogReaderTests
{
    // This test is a placeholder for future tests that will validate the functionality of the DecryptedLogReader class.
    private const string LogFilePath =
        @"D:\repos\serilog-sinks-file-encrypt\examples\Example.Console\bin\Debug\net8.0\Logs\log20260215.txt";

    private const string PrivateKeyPath =
        @"D:\repos\serilog-sinks-file-encrypt\examples\Example.Console\private_key.xml";

    private readonly Dictionary<string, string> _decryptionKeys = [];
    private readonly DecryptionOptions _options;

    public LogReaderTests()
    {
        string privateKey = System.IO.File.ReadAllText(PrivateKeyPath);
        _decryptionKeys.Add("MyKeyIdExample", privateKey);
        _options = new DecryptionOptions { DecryptionKeys = _decryptionKeys };
    }

    [Fact]
    public async Task TestDecryptedLogReader()
    {
        // Arrange
        await using var inputStream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read);
        using var outputStream = new MemoryStream();
        var decryptedLogReader = new LogReader(inputStream, _options);
        await decryptedLogReader.DecryptToStreamAsync(
            outputStream,
            TestContext.Current.CancellationToken
        );

        // Assert
        outputStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(outputStream);
        string decryptedContent = await reader.ReadToEndAsync(
            TestContext.Current.CancellationToken
        );
        Assert.False(
            string.IsNullOrEmpty(decryptedContent),
            "Decrypted content should not be empty."
        );
    }
}
