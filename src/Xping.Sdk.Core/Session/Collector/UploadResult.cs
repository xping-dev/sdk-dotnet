/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net;

namespace Xping.Sdk.Core.Session.Collector;

/// <summary>
/// Represents the result of an upload operation.
/// </summary>
/// <param name="IsSuccessful">Indicates whether the upload was successful.</param>
/// <param name="Message">A message describing the result of the upload operation.</param>
/// <param name="StatusCode">The HTTP status code received from the server, if available.</param>
/// <param name="Exception">The exception that occurred during the upload, if any.</param>
public record UploadResult(
    bool IsSuccessful,
    string Message,
    HttpStatusCode? StatusCode = null,
    Exception? Exception = null)
{
    /// <summary>
    /// Creates a successful upload result with the specified status code.
    /// </summary>
    /// <param name="statusCode">The HTTP status code received from the server.</param>
    /// <returns>A new UploadResult instance indicating success.</returns>
    public static UploadResult Success(HttpStatusCode statusCode) =>
        new(true, "Upload completed successfully", statusCode);

    /// <summary>
    /// Creates a failed upload result with the specified details.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="exception">The exception that caused the failure, if any.</param>
    /// <param name="statusCode">The HTTP status code received from the server, if available.</param>
    /// <returns>A new UploadResult instance indicating failure.</returns>
    public static UploadResult Failure(
        string message,
        Exception? exception = null,
        HttpStatusCode? statusCode = null) =>
        new(false, message, statusCode, exception);
}