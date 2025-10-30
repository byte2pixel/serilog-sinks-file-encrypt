using System.Reflection;

namespace Example.Console.Logging;

public class LoggingService
{
    public string PublicKey { get; } = GetPublicKeyXml();

    private static string GetPublicKeyXml()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("public_key.xml")) ?? throw new InvalidOperationException("public_key.xml not found as embedded resource.");
        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
