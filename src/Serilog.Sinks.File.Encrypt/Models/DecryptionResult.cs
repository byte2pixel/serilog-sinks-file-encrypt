namespace Serilog.Sinks.File.Encrypt.Models;

public class DecryptionResult
{
    public int DecryptedSessions { get; init; } = 0;
    public int DecryptedMessages { get; init; } = 0;
    public int FailedHeaders { get; init; } = 0;
    public int FailedMessages { get; init; } = 0;
    public int ResyncAttempts { get; init; } = 0;
}
