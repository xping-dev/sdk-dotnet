namespace Xping.Sdk.Validations;

/// <summary>
/// Represents errors that occur during test execution when a validation step identifies discrepancies or a lack of
/// correspondence between the actual server response and the expected value.
/// </summary>
public class ValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class.
    /// </summary>
    public ValidationException()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ValidationException(string? message) : base(message)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with a specified error message and a
    /// reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception, or a null reference if no inner exception is 
    /// specified.
    /// </param>
    public ValidationException(string message, Exception innerException) : base(message, innerException)
    { }
}
