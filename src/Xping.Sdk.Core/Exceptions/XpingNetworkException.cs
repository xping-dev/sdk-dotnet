/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Exceptions;

/// <summary>
/// Exception thrown when the Xping SDK encounters a network error during upload and strict mode is enabled.
/// </summary>
/// <remarks>
/// This exception is thrown instead of silently ignoring upload failures when
/// <see cref="Xping.Sdk.Core.Configuration.XpingConfiguration.StrictMode"/> is <see langword="true"/>
/// or the <c>XPING_STRICTMODE</c> environment variable is set to <c>true</c>.
/// The provided test framework adapters (NUnit, xUnit, MSTest) treat this as a fatal error
/// and invoke <c>Environment.FailFast</c>, terminating the test process with a clear error message.
/// </remarks>
public sealed class XpingNetworkException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XpingNetworkException"/> class.
    /// </summary>
    public XpingNetworkException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingNetworkException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public XpingNetworkException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingNetworkException"/> class
    /// with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public XpingNetworkException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
