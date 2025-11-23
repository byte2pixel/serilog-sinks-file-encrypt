namespace Serilog.Sinks.File.Encrypt.Models;

/// <summary>
/// Represents a parsed message section from the encrypted file
/// </summary>
internal record MessageSection(int MessageLength);
