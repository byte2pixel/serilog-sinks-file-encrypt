using Example.Console;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.File.Encrypt;

Console.WriteLine("Hello, World!");

KeyService keyService = new();
string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
if (!Directory.Exists(logDirectory))
{
    Directory.CreateDirectory(logDirectory);
}

Logger logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(logDirectory, "log.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 31, // Keep logs for 31 days
        hooks: new EncryptHooks(keyService.PublicKey) // Use public key for encryption
    )
    .CreateLogger();

logger.Information("Application started at {StartTime}", DateTimeOffset.Now);
for (int i = 0; i < 20; i++)
{
    logger.Information("Processing item {ItemNumber}", i + 1);
    logger.Warning("Item {ItemNumber} took longer than expected", i + 1);
}
logger.Debug("This is a debug message.");
logger.Warning("This is a warning message.");
logger.Error(new Exception("This is an error message."), "This is an error message.");
logger.Information("Application ended at {EndTime}", DateTimeOffset.Now);
await logger.DisposeAsync();
Console.WriteLine("Logs written to " + logDirectory);
