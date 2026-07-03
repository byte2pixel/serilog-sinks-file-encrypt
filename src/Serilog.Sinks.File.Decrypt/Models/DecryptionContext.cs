using System.Security.Cryptography;

namespace Serilog.Sinks.File.Decrypt.Models;

/// <summary>
/// Represents the current decryption context with active encryption keys and per-session
/// verification state.
/// </summary>
/// <param name="nonce">The AES-GCM nonce for the next data frame.</param>
/// <param name="sessionKey">The AES-GCM session key.</param>
/// <param name="version">The on-disk format version of the session (1 or 2, 0 for <see cref="Empty"/>).</param>
/// <param name="headerHash">SHA-256 hash of the raw session header bytes (v2 only; empty for v1).</param>
/// <param name="sealNonce">The reserved nonce for the end-of-log seal record (v2 only; empty for v1).</param>
/// <param name="keyId">The key ID from the session header.</param>
public class DecryptionContext(
    byte[] nonce,
    byte[] sessionKey,
    byte version = 0,
    byte[]? headerHash = null,
    byte[]? sealNonce = null,
    string keyId = ""
)
{
    /// <summary>
    /// The AES-GCM Session Key.
    /// </summary>
    public byte[] SessionKey { get; } = sessionKey;

    /// <summary>
    /// The AES-GCM Nonce (Initialization Vector) used for decryption.
    /// </summary>
    public byte[] Nonce { get; } = nonce;

    /// <summary>
    /// The on-disk format version of the session header (1 or 2; 0 for <see cref="Empty"/>).
    /// </summary>
    public byte Version { get; } = version;

    /// <summary>
    /// SHA-256 hash of the exact session header bytes as read from the stream. Bound into every
    /// v2 record's AES-GCM associated data to tie frames to their session. Empty for v1 sessions.
    /// </summary>
    public byte[] HeaderHash { get; } = headerHash ?? [];

    /// <summary>
    /// The reserved nonce (initial session nonce counter − 1) used to authenticate the
    /// end-of-log seal record independently of how many data frames were decrypted.
    /// Empty for v1 sessions.
    /// </summary>
    public byte[] SealNonce { get; } = sealNonce ?? [];

    /// <summary>
    /// The key ID from the session header.
    /// </summary>
    public string KeyId { get; } = keyId;

    /// <summary>
    /// The sequence number expected for the next data frame (equals the number of frames
    /// successfully decrypted so far). Bound into each v2 frame's associated data.
    /// </summary>
    internal ulong FrameSequence { get; set; }

    /// <summary>
    /// Whether the session's end-of-log seal record has been encountered.
    /// </summary>
    internal bool SealSeen { get; set; }

    /// <summary>
    /// The frame count declared by an authenticated seal record, when one was decrypted.
    /// </summary>
    internal ulong? DeclaredFrameCount { get; set; }

    /// <summary>
    /// Running seal status for the session; resolved to a final value when the session ends.
    /// </summary>
    internal SealStatus SealStatus { get; set; }

    /// <summary>
    /// Number of messages successfully decrypted from this session.
    /// </summary>
    internal int DecryptedMessages { get; set; }

    /// <summary>
    /// Number of message decryption failures attributed to this session.
    /// </summary>
    internal int FailedMessages { get; set; }

    /// <summary>
    /// Creates an empty decryption content.
    /// </summary>
    public static DecryptionContext Empty => new([], []);

    /// <summary>
    /// Returns true if the Nonce and SessionKey buffers are present (non-empty).
    /// Note this only reflects buffer presence, not cryptographic validity: after <see cref="Clear"/>
    /// the buffers remain non-empty (so this stays true) even though the key material has been wiped.
    /// </summary>
    public bool HasKeys => Nonce.Length > 0 && SessionKey.Length > 0;

    /// <summary>
    /// Zeroes the session key and nonces so this sensitive key material does not linger in managed
    /// memory after the session has been processed.
    /// </summary>
    public void Clear()
    {
        CryptographicOperations.ZeroMemory(SessionKey);
        CryptographicOperations.ZeroMemory(Nonce);
        CryptographicOperations.ZeroMemory(SealNonce);
    }
}
