namespace Serilog.Sinks.File.Decrypt.Models;

/// <summary>
/// The end-of-log seal verification status of a decrypted session.
/// Sessions written in the v2 format end with an authenticated seal record carrying the final
/// frame count when the log was closed cleanly; the seal is what makes truncation of a cleanly
/// closed log detectable.
/// </summary>
public enum SealStatus
{
    /// <summary>
    /// The session was written in the v1 format, which has no seal record. Completeness of the
    /// session cannot be verified.
    /// </summary>
    NotApplicable = 0,

    /// <summary>
    /// The seal record was present, authenticated successfully, and its declared frame count
    /// matches the number of decrypted frames. The session is complete: it was closed cleanly
    /// and no trailing frames were removed.
    /// </summary>
    Sealed = 1,

    /// <summary>
    /// No seal record (or only a partially written one) was found at the end of the session.
    /// The writer did not close cleanly OR the tail of the log was truncated — the two are
    /// byte-for-byte indistinguishable by design. The decrypted messages themselves are
    /// authentic, but the session cannot be verified as complete.
    /// </summary>
    Unsealed = 2,

    /// <summary>
    /// The seal record authenticated successfully but declares a different frame count than was
    /// decrypted. Data frames were removed from (or lost at) the tail of a cleanly closed log —
    /// this is the specific fingerprint of truncation of a sealed session.
    /// </summary>
    SealCountMismatch = 3,

    /// <summary>
    /// The seal record failed authentication, a duplicate seal was encountered, or additional
    /// records appeared after the seal. Indicates tampering with or corruption of the session's
    /// tail.
    /// </summary>
    SealInvalid = 4,
}
