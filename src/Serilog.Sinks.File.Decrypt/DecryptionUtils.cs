using Serilog.Sinks.File.Decrypt.Models;

namespace Serilog.Sinks.File.Decrypt;

/// <summary>
/// Convenience utilities for decrypting log files encrypted with Serilog.Sinks.File.Encrypt.
/// </summary>
public static class DecryptionUtils
{
    /// <summary>
    /// Decrypts an encrypted log file from an input stream and writes the decrypted content to an output stream.
    /// </summary>
    /// <param name="inputStream">The input stream to decrypt.</param>
    /// <param name="outputStream">The output stream for plaintext.</param>
    /// <param name="options">Decryption options.</param>
    /// <param name="logger">Optional audit logger for decryption errors. If provided, decryption errors will be logged with details about the error and the position in the stream where it occurred.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the decryption operation.</param>
    /// <returns>
    /// A <see cref="DecryptionResult"/> containing counts of decrypted sessions, messages, failures, and resync attempts.
    /// </returns>
    public static async Task<DecryptionResult> DecryptLogFileAsync(
        Stream inputStream,
        Stream outputStream,
        DecryptionOptions options,
        ILogger? logger = null,
        CancellationToken cancellationToken = default
    )
    {
        using LogReader reader = new(inputStream, options, logger);
        return await reader.DecryptToStreamAsync(outputStream, cancellationToken);
    }

    /// <summary>
    /// Decrypts an encrypted log file and writes the decrypted content to a new file.
    /// </summary>
    /// <param name="encryptedFilePath">Path to the encrypted log file. File must exist and be readable.</param>
    /// <param name="outputFilePath">Path where the decrypted content will be written. Will be created or overwritten.</param>
    /// <param name="options">Streaming options for the decryption process.</param>
    /// <param name="logger">Optional audit logger for decryption errors. If provided, decryption errors will be logged with details about the error and the position in the file where it occurred.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the decryption operation.</param>
    /// <returns>A task representing the asynchronous decryption operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="encryptedFilePath"/> or <paramref name="outputFilePath"/> cannot be opened or created.</exception>
    /// <exception cref="ArgumentNullException">Thrown when any string parameter is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when <paramref name="encryptedFilePath"/> does not exist.</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">Thrown when decryption fails due to wrong key, corrupted data, or invalid format.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the input file does not contain valid encryption markers.</exception>
    /// <exception cref="IOException">Thrown when file access fails due to permissions or locking issues.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This is a convenience overload that automatically manages file streams. For more control over stream handling,
    /// use the <see cref="DecryptLogFileAsync(Stream, Stream, DecryptionOptions, ILogger?, CancellationToken)"/> overload.
    /// </remarks>
    public static async Task<DecryptionResult> DecryptLogFileAsync(
        string encryptedFilePath,
        string outputFilePath,
        DecryptionOptions options,
        ILogger? logger = null,
        CancellationToken cancellationToken = default
    )
    {
        await using FileStream inputStream = System.IO.File.OpenRead(encryptedFilePath);
        await using FileStream outputStream = System.IO.File.Create(outputFilePath);

        return await DecryptLogFileAsync(
            inputStream,
            outputStream,
            options,
            logger,
            cancellationToken
        );
    }
}
