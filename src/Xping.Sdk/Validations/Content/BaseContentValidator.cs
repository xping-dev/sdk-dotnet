/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net.Http.Headers;
using System.Text;
using Xping.Sdk.Core.Clients.Browser;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Session;

namespace Xping.Sdk.Validations.Content;

/// <summary>
/// Represents an abstract base class for validating HTTP content.
/// </summary>
/// <remarks>
/// The BaseContentValidator class inherits from the TestComponent class and provides a common method for decoding 
/// HTTP content.
/// </remarks>
public abstract class BaseContentValidator(string name) : TestComponent(name, TestStepType.ValidateStep)
{
    /// <summary>
    /// Retrieves the HttpResponseMessage from the TestContext.
    /// </summary>
    /// <param name="context">The TestContext instance containing the property bag values.</param>
    /// <returns>
    /// The HttpResponseMessage if it exists in the property bag; otherwise, the HttpResponseMessage
    /// contained within the BrowserResponseMessage, or null if neither are found.
    /// </returns>
    /// <remarks>
    /// This method first attempts to retrieve the HttpResponseMessage directly from the TestContext's
    /// non-serializable property bag. If it is not found, it then attempts to retrieve the
    /// BrowserResponseMessage from the property bag and return its associated HttpResponseMessage.
    /// </remarks>
    protected virtual HttpResponseMessage? GetHttpResponseMessage(TestContext context)
    {
        return context.GetNonSerializablePropertyBagValue<HttpResponseMessage>(PropertyBagKeys.HttpResponseMessage) ??
               context
                   .GetNonSerializablePropertyBagValue<BrowserResponseMessage>(PropertyBagKeys.BrowserResponseMessage)?
                   .HttpResponseMessage;
    }

    /// <summary>
    /// Retrieves the BrowserResponseMessage from the TestContext.
    /// </summary>
    /// <param name="context">The TestContext instance containing the property bag values.</param>
    /// <returns>
    /// The BrowserResponseMessage if it exists in the property bag; otherwise, null.
    /// </returns>
    /// <remarks>
    /// This method attempts to retrieve the BrowserResponseMessage from the TestContext's
    /// non-serializable property bag. If it is not found, the method returns null.
    /// </remarks>
    protected virtual BrowserResponseMessage? GetBrowserResponseMessage(TestContext context)
    {
        return context
            .GetNonSerializablePropertyBagValue<BrowserResponseMessage>(PropertyBagKeys.BrowserResponseMessage);
    }

    /// <summary>
    /// Retrieves the data (as a byte array) from the TestContext.
    /// </summary>
    /// <param name="context">The TestContext instance containing the property bag values.</param>
    /// <returns>
    /// The data as a byte array if it exists in the property bag; otherwise, an empty byte array.
    /// </returns>
    /// <remarks>
    /// This method attempts to retrieve the data from the TestContext's property bag. If the data is not found,
    /// it returns an empty byte array.
    /// </remarks>
    protected virtual byte[] GetData(TestContext context)
    {
        return context.GetPropertyBagValue<byte[]>(PropertyBagKeys.HttpContent) ?? [];
    }
    
    /// <summary>
    /// Decodes the HTTP content from a byte array and content headers.
    /// </summary>
    /// <param name="data">The byte array that contains the HTTP content.</param>
    /// <param name="contentHeaders">
    /// The content headers that specify the encoding and media type of the content.
    /// </param>
    /// <returns>A string representation of the HTTP content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the data or contentHeaders parameter is null.</exception>
    /// <exception cref="DecoderFallbackException">Thrown when a decoder fallback operation fails.</exception>
    protected virtual string GetContent(byte[] data, HttpContentHeaders contentHeaders)
    {
        ArgumentNullException.ThrowIfNull(data, nameof(data));
        ArgumentNullException.ThrowIfNull(contentHeaders, nameof(contentHeaders));

        foreach (string encoding in contentHeaders.ContentEncoding)
        {
            try
            {
                string contentString = Encoding.GetEncoding(encoding).GetString(data);
                return contentString;
            }
            catch (Exception)
            {
                // Unable to decode content with this encoding, try the next one
            }
        }

        try
        {
            // Fallback to content-type header
            if (contentHeaders.ContentType?.CharSet != null)
            {
                return Encoding.GetEncoding(contentHeaders.ContentType.CharSet).GetString(data);
            }
        }
        catch (Exception)
        {
            // ignored
        }

        // Fallback to UTF-8
        return Encoding.UTF8.GetString(data);
    }
}
