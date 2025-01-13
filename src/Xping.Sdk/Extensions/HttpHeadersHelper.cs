/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace Xping.Sdk.Extensions;

internal static class HttpHeadersHelper
{
    /// <summary>
    /// Return all headers as string similar to: 
    /// {"HeaderName1": "Value1", "HeaderName1": "Value2", "HeaderName2": "Value1"}
    /// </summary>
    public static string DumpHeaders(params HttpHeaders[] headers)
    {
        var sb = new StringBuilder();
        sb.Append('{');

        bool firstItem = true;
        for (int i = 0; i < headers.Length; i++)
        {
            if (headers[i] != null)
            {
                foreach (var header in headers[i])
                {
                    foreach (var headerValue in header.Value)
                    {
                        if (firstItem != true)
                        {
                            sb.Append(',');
                        }

                        sb.AppendFormat(CultureInfo.InvariantCulture, "{0}", header.Key);
                        sb.Append(": ");
                        sb.AppendFormat(CultureInfo.InvariantCulture, "{0}", headerValue);
                        firstItem = false;
                    }
                }
            }
        }
        sb.Append('}');

        return sb.ToString();
    }
}
