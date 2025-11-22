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
        const string originalText = "Hello, simple encrypted log!";
        string tempFile = Path.GetTempFileName();
        using (FileStream fs = System.IO.File.Create(tempFile))
        using (RSA rsa = RSA.Create())
        {
            rsa.FromXmlString(publicKey);
            using (EncryptedStream encStream = new(fs, rsa))
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
    public void DecryptLogFile_ReturnsMultipleDecryptedEntries()
    {
        // Arrange
        (string publicKey, string privateKey) = EncryptionUtils.GenerateRsaKeyPair();
        const string logMessage1 = "Simple log file test!";
        const string logMessage2 = "Second simple log entry!";
        string tempFile = Path.GetTempFileName();
        using (FileStream fs = System.IO.File.Create(tempFile))
        using (RSA rsa = RSA.Create())
        {
            rsa.FromXmlString(publicKey);
            using (EncryptedStream encStream = new(fs, rsa))
            {
                byte[] data1 = Encoding.UTF8.GetBytes(logMessage1);
                encStream.Write(data1, 0, data1.Length);
                encStream.Flush();
                
                byte[] data2 = Encoding.UTF8.GetBytes(logMessage2);
                encStream.Write(data2, 0, data2.Length);
                encStream.Flush();
            }
        }
        
        // Act
        string decrypted = EncryptionUtils.DecryptLogFile(tempFile, privateKey);
        
        // Assert
        decrypted.ShouldContain(logMessage1);
        decrypted.ShouldContain(logMessage2);
        System.IO.File.Delete(tempFile);
    }
}
