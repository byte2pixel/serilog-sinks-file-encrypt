namespace Serilog.Sinks.File.Decrypt.Interfaces;

/// <summary>
/// The IKeyProvider interface defines the contract for decrypting the random AES-GCM key and nonce.
/// Implementations of this interface are responsible for providing the logic to retrieve the appropriate
/// decryption key based on the provided key ID and using it to decrypt the given cipher text.
/// </summary>
public interface IKeyProvider
{
    /// <summary>
    /// Decrypts the given cipher text using the decryption key associated with the provided key ID.
    /// The cipher text is expected to contain the AES-GCM encrypted session key and nonce, which are necessary for
    /// decrypting the log entries. Implementations of this method should handle the logic for decrypting
    /// the cipher text and returning the decrypted session key and nonce as a byte array.
    /// </summary>
    /// <param name="keyId">The key id that was used to encrypt the AES-GCM session key and nonce</param>
    /// <param name="cipherText">The AES-GCM encrypted session key and nonce that needs to be decrypted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A byte array containing the decrypted AES-GCM session key and nonce.
    /// </returns>
    Task<byte[]> DecryptAsync(
        string keyId,
        ReadOnlyMemory<byte> cipherText,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Returns the key size in bits for the RSA key associated with the provided key ID.
    /// This information is necessary to determine the expected size of the encrypted session key and nonce
    /// in the header, which is typically equal to the RSA key size divided by 8 (to convert from bits to bytes).
    /// Implementations of this method should retrieve the appropriate RSA key based on the key ID and return its key size in bits.
    /// </summary>
    /// <param name="keyId">The key id that was used to encrypt the AES-GCM session key and nonce</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An integer representing the key size in bits for the RSA key associated with the provided key ID.
    /// </returns>
    /// <remarks>
    /// Implementations should either build the sizes at construction or cache them after the first retrieval
    /// to avoid expensive operations on subsequent calls, as the key size is expected to be constant for a given
    /// key ID.
    /// </remarks>
    Task<int> GetKeySizeAsync(string keyId, CancellationToken cancellationToken = default);
}
