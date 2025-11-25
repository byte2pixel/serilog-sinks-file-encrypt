using System.Diagnostics.CodeAnalysis;

namespace Serilog.Sinks.File.Encrypt.Tests.unit;

public class EncryptionUtilsTests
{
    [Fact]
    public void GenerateRsaKeyPair_ReturnsValidKeys()
    {
        // Arrange, Act
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();

        using RSA privateKeyRsa = RSA.Create();
        privateKeyRsa.FromXmlString(privateKey);

        using RSA publicKeyRsa = RSA.Create();
        publicKeyRsa.FromXmlString(publicKey);

        byte[] data = "ABCD"u8.ToArray();
        byte[] encryptedData = publicKeyRsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
        byte[] decryptedData = privateKeyRsa.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);

        // Assert
        decryptedData.ShouldBe(data);
    }

    [SuppressMessage("csharpsquid:S6966", "Tests should use async suffix where applicable")]
    [Fact]
    public async Task DecryptLogFileAsync_ReturnsDecryptedContent()
    {
        // Arrange
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();
        const string originalText = "Hello, simple encrypted log!";
        string tempFile = Path.GetTempFileName();

        try
        {
            await using (FileStream fs = System.IO.File.Create(tempFile))
            using (RSA rsa = RSA.Create())
            {
                rsa.FromXmlString(publicKey);
                await using (EncryptedStream encStream = new(fs, rsa))
                {
                    byte[] data = Encoding.UTF8.GetBytes(originalText);
#pragma warning disable S6966 // Awaitable method should be used
                    encStream.Write(data, 0, data.Length);
                    encStream.Flush();
#pragma warning restore S6966 // Awaitable method should be used
                }
            }

            // Act
            await using (FileStream inputStream = System.IO.File.OpenRead(tempFile))
            using (MemoryStream outputStream = new())
            {
                await EncryptionUtils.DecryptLogFileAsync(
                    inputStream,
                    outputStream,
                    privateKey,
                    cancellationToken: TestContext.Current.CancellationToken
                );

                // Assert
                outputStream.Position = 0;
                using StreamReader reader = new(outputStream);
                string decrypted = await reader.ReadToEndAsync(
                    TestContext.Current.CancellationToken
                );
                decrypted.ShouldContain(originalText);
            }
        }
        finally
        {
            try
            {
                System.IO.File.Delete(tempFile);
            }
            catch
            { /* Ignore cleanup errors */
            }
        }
    }

    [Fact]
    public async Task DecryptLogFileAsync_ReturnsMultipleDecryptedEntries()
    {
        // Arrange
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();
        const string logMessage1 = "Simple log file test!";
        const string logMessage2 = "Second simple log entry!";
        string tempFile = Path.GetTempFileName();

        try
        {
            await using (FileStream fs = System.IO.File.Create(tempFile))
            using (RSA rsa = RSA.Create())
            {
                rsa.FromXmlString(publicKey);
                await using (EncryptedStream encStream = new(fs, rsa))
                {
                    byte[] data1 = Encoding.UTF8.GetBytes(logMessage1);
                    await encStream.WriteAsync(data1, TestContext.Current.CancellationToken);
                    await encStream.FlushAsync(TestContext.Current.CancellationToken);

                    byte[] data2 = Encoding.UTF8.GetBytes(logMessage2);
                    await encStream.WriteAsync(data2, TestContext.Current.CancellationToken);
                    await encStream.FlushAsync(TestContext.Current.CancellationToken);
                }
            }

            // Act
            await using (FileStream inputStream = System.IO.File.OpenRead(tempFile))
            using (MemoryStream outputStream = new())
            {
                await EncryptionUtils.DecryptLogFileAsync(
                    inputStream,
                    outputStream,
                    privateKey,
                    cancellationToken: TestContext.Current.CancellationToken
                );

                // Assert
                outputStream.Position = 0;
                using StreamReader reader = new(outputStream);
                string decrypted = await reader.ReadToEndAsync(
                    TestContext.Current.CancellationToken
                );
                decrypted.ShouldContain(logMessage1);
                decrypted.ShouldContain(logMessage2);
            }
        }
        finally
        {
            try
            {
                System.IO.File.Delete(tempFile);
            }
            catch
            { /* Ignore cleanup errors */
            }
        }
    }
}
