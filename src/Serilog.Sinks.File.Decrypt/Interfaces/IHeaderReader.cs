namespace Serilog.Sinks.File.Decrypt.Interfaces;

/// <summary>
/// The <see cref="IHeaderReader"/> interface defines the contract for decoding the session header information.
/// </summary>
internal interface IHeaderReader
{
    /// <summary>
    /// Decrypts the session header information, which includes the RSA-encrypted session key and nonce.
    /// </summary>
    /// <param name="keyProvider">Provides a method for decrypting the AES-GCM session key and nonce.</param>
    /// <param name="keyId">The key id that was used to encrypt the AES-GCM session key and nonce</param>
    /// <param name="headerData">The encrypted header data read from the log file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the decrypted AES session key and nonce.</returns>
    internal Task<(byte[] AesKey, byte[] Nonce)> Decrypt(
        IKeyProvider keyProvider,
        string keyId,
        ReadOnlyMemory<byte> headerData,
        CancellationToken cancellationToken = default
    );
}
