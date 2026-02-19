namespace Serilog.Sinks.File.Encrypt;

/// <summary>
/// Base exception for encryption-related errors.
/// </summary>
public class DecryptionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DecryptionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DecryptionException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecryptionException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="inner">The inner exception.</param>
    public DecryptionException(string message, Exception inner)
        : base(message, inner) { }
}

/// <summary>
/// Exception thrown when a session header is encountered while reading messages.
/// </summary>
public class SessionHeaderEncounteredException : DecryptionException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionHeaderEncounteredException"/> class with a default error message.
    /// </summary>
    public SessionHeaderEncounteredException()
        : base("Session header encountered while reading messages.") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionHeaderEncounteredException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public SessionHeaderEncounteredException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionHeaderEncounteredException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="inner">The inner exception.</param>
    public SessionHeaderEncounteredException(string message, Exception inner)
        : base(message, inner) { }
}
