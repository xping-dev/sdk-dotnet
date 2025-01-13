/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net;

namespace Xping.Sdk.Core.Clients.Browser;

/// <summary>
/// Represents errors that occur when an HTTP request is redirected more times than the set limit.
/// </summary>
/// <remarks>
/// This exception is thrown when an HTTP operation encounters more redirects than the application allows.
/// It inherits from the <see cref="WebException"/> class and adds no new functionality; instead, it provides a more 
/// specific exception type to handle excessive HTTP redirection scenarios.
/// </remarks>
public class TooManyRedirectsException : WebException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TooManyRedirectsException"/> class.
    /// </summary>
    public TooManyRedirectsException()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TooManyRedirectsException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public TooManyRedirectsException(string message)
        : base(message)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TooManyRedirectsException"/> class with a specified error message 
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">
    /// The exception that is the cause of the current exception, or a null reference if no inner 
    /// exception is specified.
    /// </param>
    public TooManyRedirectsException(string message, Exception inner)
        : base(message, inner)
    { }
}
