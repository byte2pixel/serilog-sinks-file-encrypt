#:project ../../src/Serilog.Sinks.File.Encrypt/Serilog.Sinks.File.Encrypt.csproj

using Serilog;
using Serilog.Core;
using Serilog.Sinks.File.Encrypt;

string publicKey = File.ReadAllText("public_key.xml");

Logger logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        path: "file-based.log",
        hooks: new EncryptHooks(publicKey, keyId: "file-based-key")
    )
    .CreateLogger();

logger.Information("[vLatest] This is a test log message.");
for (int i = 0; i < 10; i++)
{
    logger.Information("[vLatest] This is test log message number {LogNumber}.", i);
}
logger.Debug("[vLatest] This is a debug log message.");
logger.Error("[vLatest] This is an error log message.");
await logger.DisposeAsync();
Console.WriteLine("[vLatest] Encrypted v2-format session appended to file-based.log");
