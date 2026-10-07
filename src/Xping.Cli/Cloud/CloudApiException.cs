/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Cloud;

/// <summary>
/// The DataGateway refused a read in a way retrying does not fix (cli-auth-cli-spec §9.7).
/// </summary>
/// <remarks>The message is worded for the user and never holds a credential.</remarks>
internal sealed class CloudApiException : Exception
{
    public CloudApiException()
    {
    }

    public CloudApiException(string message)
        : base(message)
    {
    }

    public CloudApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public CloudApiException(int statusCode, string? title, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Title = title;
    }

    /// <summary>
    /// Gets the HTTP status of the answer, or 0 when there was none.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the problem's <c>title</c>, the stable error code (contract §8.2), when the server sent one.
    /// </summary>
    public string? Title { get; }
}
