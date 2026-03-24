/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Exceptions;

/// <summary>
/// Exception thrown when the Xping SDK configuration is invalid and strict mode is enabled.
/// </summary>
/// <remarks>
/// This exception is thrown instead of silently disabling the SDK when
/// <see cref="Xping.Sdk.Core.Configuration.XpingConfiguration.StrictMode"/> is <see langword="true"/>
/// or the <c>XPING_STRICTMODE</c> environment variable is set to <c>true</c>.
/// </remarks>
public sealed class XpingConfigurationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XpingConfigurationException"/> class.
    /// </summary>
    public XpingConfigurationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingConfigurationException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public XpingConfigurationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingConfigurationException"/> class
    /// with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public XpingConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
