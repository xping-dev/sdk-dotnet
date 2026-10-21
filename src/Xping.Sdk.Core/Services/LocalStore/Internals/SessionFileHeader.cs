/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xping.Sdk.Core.Models;

namespace Xping.Sdk.Core.Services.LocalStore.Internals;

/// <summary>
/// Reads the assemblies a stored session covers from the head of its file.
/// </summary>
/// <remarks>
/// <para>
/// Used where a file's name does not say which assemblies it covers — a session too wide to tag, see
/// <see cref="SessionFileName"/> — and where the names themselves are needed rather than their tags,
/// as <c>xping where</c> needs them. Deserializing the session for that would decompress every
/// execution in it, up to megabytes of JSON, to read a handful of names.
/// <see cref="TestSession.Assemblies"/> is serialized first, so the names sit in the first few
/// hundred bytes and the read stops there.
/// </para>
/// <para>
/// A file whose first property is anything else, or that cannot be read, is reported as unknown
/// rather than scanned. The caller counts it as naming no assembly.
/// </para>
/// </remarks>
internal static class SessionFileHeader
{
    // The head is read in one bounded piece. A list of assembly names that has not ended within it
    // is not a header worth finishing — 64 KB holds a thousand names — and is reported as unknown.
    private const int HeaderBytes = 64 * 1024;

    private static readonly byte[] AssembliesProperty = Encoding.UTF8.GetBytes("assemblies");

    /// <summary>
    /// Returns the assemblies recorded at the head of a session file.
    /// </summary>
    /// <param name="path">The session file.</param>
    /// <returns>
    /// The assembly names as stored, or <see langword="null"/> when the file cannot be read or does
    /// not open with them.
    /// </returns>
    public static IReadOnlyList<string>? ReadAssemblies(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);

            return Read(gzip);
        }
        // InvalidOperationException is GetString on a name holding an invalid escape: a damaged
        // file, which has to cost one unknown header rather than escape into retention.
        catch (Exception ex) when (
            StoreRetention.IsStorageFailure(ex)
            || ex is InvalidDataException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static List<string>? Read(Stream stream)
    {
        byte[] buffer = new byte[HeaderBytes];
        int length = 0;
        int read;

        while (length < buffer.Length && (read = stream.Read(buffer, length, buffer.Length - length)) > 0)
            length += read;

        // A file shorter than the buffer has been read whole, so the reader may treat it as final
        // and report truncation as an error rather than as a request for more data.
        var reader = new Utf8JsonReader(
            new ReadOnlySpan<byte>(buffer, 0, length), isFinalBlock: length < buffer.Length, state: default);

        // Only the first property is looked at; see the class remarks.
        if (!Next(ref reader, JsonTokenType.StartObject)
            || !Next(ref reader, JsonTokenType.PropertyName)
            || !reader.ValueTextEquals(AssembliesProperty)
            || !Next(ref reader, JsonTokenType.StartArray))
        {
            return null;
        }

        var assemblies = new List<string>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return assemblies;

            if (reader.TokenType != JsonTokenType.String)
                return null;

            assemblies.Add(reader.GetString()!);
        }

        // The list ran past the bounded head.
        return null;
    }

    private static bool Next(ref Utf8JsonReader reader, JsonTokenType expected) =>
        reader.Read() && reader.TokenType == expected;
}
