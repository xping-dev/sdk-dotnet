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
/// Retention counts runs per assembly, so it has to know which assemblies every file covers, on every
/// write. Deserializing each session for that would decompress every execution in the store — up to
/// megabytes of JSON per file — to read a handful of names. <see cref="TestSession.Assemblies"/> is
/// serialized first, so the names sit in the first few hundred bytes and the read stops there.
/// </para>
/// <para>
/// A file whose first property is anything else was not written in that order, and is reported as
/// unknown rather than scanned: the caller falls back to a full read, which is always correct, just
/// slower.
/// </para>
/// </remarks>
internal static class SessionFileHeader
{
    private const int InitialBufferSize = 4 * 1024;

    // A header is a list of assembly names. One that has not ended by here is not a header worth
    // finishing; the full read decides instead.
    private const int MaxHeaderBytes = 1024 * 1024;

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
        catch (Exception ex) when (
            StoreRetention.IsStorageFailure(ex) || ex is InvalidDataException or JsonException)
        {
            return null;
        }
    }

    private static List<string>? Read(Stream stream)
    {
        byte[] buffer = new byte[InitialBufferSize];
        int length = 0;
        int total = 0;
        bool isFinal = false;
        JsonReaderState state = default;

        bool inArray = false;
        var assemblies = new List<string>();

        while (!isFinal)
        {
            // A token larger than what is left of the buffer: make room rather than stall on it.
            if (length == buffer.Length)
                Array.Resize(ref buffer, buffer.Length * 2);

            int read = stream.Read(buffer, length, buffer.Length - length);
            length += read;
            total += read;
            isFinal = read == 0;

            if (total > MaxHeaderBytes)
                return null;

            var reader = new Utf8JsonReader(new ReadOnlySpan<byte>(buffer, 0, length), isFinal, state);

            while (reader.Read())
            {
                if (inArray)
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.String:
                            assemblies.Add(reader.GetString()!);
                            break;
                        case JsonTokenType.EndArray:
                            return assemblies;
                        default:
                            return null;
                    }

                    continue;
                }

                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject when reader.CurrentDepth == 0:
                        break;
                    case JsonTokenType.PropertyName when reader.CurrentDepth == 1:
                        // Only the first property is looked at; see the class remarks.
                        if (!reader.ValueTextEquals(AssembliesProperty))
                            return null;
                        break;
                    case JsonTokenType.StartArray when reader.CurrentDepth == 1:
                        inArray = true;
                        break;
                    default:
                        return null;
                }
            }

            state = reader.CurrentState;

            int consumed = (int)reader.BytesConsumed;
            Buffer.BlockCopy(buffer, consumed, buffer, 0, length - consumed);
            length -= consumed;
        }

        return null;
    }
}
