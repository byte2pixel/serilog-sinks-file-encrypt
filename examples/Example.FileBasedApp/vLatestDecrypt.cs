#:project ../../src/Serilog.Sinks.File.Decrypt/Serilog.Sinks.File.Decrypt.csproj

using Serilog.Sinks.File.Decrypt;
using Serilog.Sinks.File.Decrypt.Models;

string privateKey = File.ReadAllText("private_key.xml");

var options = new DecryptionOptions
{
    KeyProvider = new LocalKeyProvider("file-based-key", privateKey),
};

DecryptionResult result = await DecryptionUtils.DecryptLogFileAsync(
    "file-based.log",
    "file-based-decrypted.log",
    options
);

if (result.NothingDecrypted)
{
    Console.WriteLine("Nothing was decrypted — wrong key, wrong key ID, or not an encrypted log.");
    return 1;
}

Console.WriteLine(
    $"Decrypted {result.DecryptedMessages} message(s) from {result.DecryptedSessions} session(s):"
);
foreach (SessionResult session in result.Sessions)
{
    Console.WriteLine(
        $"  Session {session.Index}: format v{session.FormatVersion}, "
            + $"{session.DecryptedMessages} message(s), seal: {session.SealStatus}"
    );
}

if (result.FailedHeaders > 0 || result.FailedMessages > 0)
{
    Console.WriteLine(
        $"Failures: {result.FailedHeaders} header(s), {result.FailedMessages} message(s)."
    );
}

Console.WriteLine("Decrypted log written to file-based-decrypted.log");
return 0;
