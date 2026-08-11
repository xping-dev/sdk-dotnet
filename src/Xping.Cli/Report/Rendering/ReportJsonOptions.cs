/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xping.Cli.Report.Contract;

/// <summary>
/// How the report's JSON is written.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately separate from the SDK's serializer options. Those describe a wire format the cloud
/// consumes and are free to change with it; this describes a documented output contract a user's
/// script depends on, and the two must be able to move independently.
/// </para>
/// <para>
/// Indented output, because a person reads this from a terminal at least as often as a script pipes
/// it. Nulls are written rather than dropped so a consumer can tell "no value" apart from "field
/// removed in a newer version".
/// </para>
/// </remarks>
internal static class ReportJsonOptions
{
    /// <summary>Gets the options used for every report payload.</summary>
    public static JsonSerializerOptions Default { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,

            // Not relaxed encoding: this output is frequently pasted into HTML dashboards and chat
            // clients, and error text routinely contains angle brackets and ampersands.
            Encoder = JavaScriptEncoder.Default,

            // Values are already rounded by the envelope builder, so no converter needs to do it
            // here — and one that did would be a second place precision is decided.
            NumberHandling = JsonNumberHandling.Strict
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        return options;
    }
}
