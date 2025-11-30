using Serilog.Sinks.File.Encrypt;

namespace Example.WebApi;

public static class EncryptSetup
{
    public static EncryptHooks SetupHook
    {
        get
        {
            string environment =
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .Build();

            string? rsaPublicKey = configuration["LogPublicKey"];

            return string.IsNullOrWhiteSpace(rsaPublicKey)
                ? throw new InvalidOperationException("LogPublicKey not found in configuration")
                : new EncryptHooks(rsaPublicKey);
        }
    }
}
