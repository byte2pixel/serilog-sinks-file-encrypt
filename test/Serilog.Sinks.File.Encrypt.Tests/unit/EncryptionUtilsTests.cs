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

    [Fact]
    public void DecryptLogFile_ReturnsDecryptedContent()
    {
        // Arrange
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();
        const string originalText = "Hello, encrypted log!";
        string tempFile = Path.GetTempFileName();
        using (FileStream fs = System.IO.File.Create(tempFile))
        using (RSA rsa = RSA.Create())
        {
            rsa.FromXmlString(publicKey);
            using (EncryptedChunkStream encStream = new(fs, rsa))
            {
                byte[] data = Encoding.UTF8.GetBytes(originalText);
                encStream.Write(data, 0, data.Length);
                encStream.Flush();
            }
        }
        
        // Act
        string decrypted = EncryptionUtils.DecryptLogFile(tempFile, privateKey);
        
        // Assert
        decrypted.ShouldContain(originalText);
        System.IO.File.Delete(tempFile);
    }

    [Fact]
    public void DecryptLogFileToFile_WritesDecryptedContentToFile()
    {
        // Arrange
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();
        const string logMessage1 = "Log file to file test!";
        const string logMessage2 = "Second log entry!";
        string inputFile = Path.GetTempFileName();
        string outputFile = Path.GetTempFileName();
        using (RSA rsa = RSA.Create())
        {
            rsa.FromXmlString(publicKey);
            using (FileStream fs = System.IO.File.Create(inputFile))
            {
                using (EncryptedChunkStream encStream = new(fs, rsa))
                {
                    byte[] data = Encoding.UTF8.GetBytes(logMessage1);
                    encStream.Write(data, 0, data.Length);
                    encStream.Flush();
                }
            }

            using (FileStream fs = System.IO.File.Open(inputFile, FileMode.Append))
            {
                using (EncryptedChunkStream encStream = new(fs, rsa))
                {
                    byte[] data = Encoding.UTF8.GetBytes(logMessage2);
                    encStream.Write(data, 0, data.Length);
                    encStream.Flush();
                }
            }
        }
        
        // Act
        EncryptionUtils.DecryptLogFileToFile(inputFile, outputFile, privateKey);
        string result = System.IO.File.ReadAllText(outputFile);
        
        // Assert
        result.ShouldContain(logMessage1);
        result.ShouldContain(logMessage2);
        System.IO.File.Delete(inputFile);
        System.IO.File.Delete(outputFile);
    }
}
