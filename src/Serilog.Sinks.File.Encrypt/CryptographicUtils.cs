namespace System.Security.Cryptography;

/// <summary>
/// <see cref="RSA" /> extension to support multiple key formats.
/// </summary>
public static class CryptographicUtils
{
    /// <summary>
    /// Imports an RSA key into an <see cref="RSA"/> instance from a string in either XML or PEM format.
    /// </summary>
    /// <param name="rsa">The <see cref="RSA"/> instance to import the key into.</param>
    /// <param name="key">The RSA key as a string.</param>
    /// <exception cref="CryptographicException">Unknown or invalid key format.</exception>
    /// <exception cref="FormatException">Invalid XML key format.</exception>
    /// <exception cref="ArgumentException">Invalid PEM key format.</exception>
    public static void FromString(this RSA rsa, string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        switch (key[0])
        {
            case '<':
                rsa.FromXmlString(key);
                break;
            case '-':
                rsa.ImportFromPem(key);
                break;
            default:
                throw new CryptographicException(
                    "Invalid RSA key format. Key must be in XML or PEM format."
                );
        }
    }
}
