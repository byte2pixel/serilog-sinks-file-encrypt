using System.Reflection;
using System.Security.Cryptography;

namespace Example.Console.Logging;

public class LoggingService
{
    public RSA PublicKey { get; }

    public LoggingService()
    {
        PublicKey = RSA.Create();
        PublicKey.FromXmlString(GetPublicKeyXml().GetAwaiter().GetResult());
    }
    
    private static async Task<string> GetPublicKeyXml()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("public_key.xml")) ?? throw new InvalidOperationException("public_key.xml not found as embedded resource.");
        await using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}