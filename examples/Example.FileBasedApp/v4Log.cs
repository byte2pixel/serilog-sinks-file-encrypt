#:package Serilog.Sinks.File.Encrypt@4.0.0

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

logger.Information("[v4] This is a test log message.");
for (int i = 0; i < 10; i++)
{
    logger.Information("[v4] This is test log message number {LogNumber}.", i);
}
logger.Debug("[v4] This is a debug log message.");
logger.Error("[v4] This is an error log message.");
await logger.DisposeAsync();
Console.WriteLine("[v4] Encrypted v1-format session appended to file-based.log");
