/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;

namespace Xping.Sdk.Core.Services.Serialization.Internals;

/// <summary>
/// Provides JSON serialization and deserialization services for the Xping SDK using System.Text.Json.
/// This class implements the <see cref="IXpingSerializer"/> interface and provides consistent
/// serialization behavior across all components of the SDK.
/// </summary>
/// <remarks>
/// This serializer is thread-safe and can be used as a singleton.
/// It uses the standard Xping serialization options defined in <see cref="XpingSerializerOptions"/>.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="XpingJsonSerializer"/> class with custom options.
/// </remarks>
/// <param name="options">The JSON serialization options to use.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
internal sealed class XpingJsonSerializer(JsonSerializerOptions options) : IXpingSerializer
{
    private readonly JsonSerializerOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc/>
    string IXpingSerializer.Serialize<T>(T value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value), "Cannot serialize null value.");
        }

        try
        {
            return JsonSerializer.Serialize(value, _options);
        }
        catch (Exception ex) when (ex is not ArgumentNullException)
        {
            throw new InvalidOperationException(
                $"Failed to serialize object of type {typeof(T).Name} to JSON.",
                ex);
        }
    }

    /// <inheritdoc/>
    byte[] IXpingSerializer.SerializeToUtf8Bytes<T>(T value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value), "Cannot serialize null value.");
        }

        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(value, _options);
        }
        catch (Exception ex) when (ex is not ArgumentNullException)
        {
            throw new InvalidOperationException(
                $"Failed to serialize object of type {typeof(T).Name} to UTF-8 bytes.",
                ex);
        }
    }

    /// <inheritdoc/>
    T? IXpingSerializer.Deserialize<T>(string json) where T : class
    {
        if (json == null)
        {
            throw new ArgumentNullException(nameof(json), "Cannot deserialize null JSON string.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, _options);
        }
        catch (JsonException)
        {
            // Re-throw JsonException as-is for callers to handle
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize JSON to type {typeof(T).Name}.",
                ex);
        }
    }

    /// <inheritdoc/>
    T? IXpingSerializer.Deserialize<T>(ReadOnlySpan<byte> utf8Json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(utf8Json, _options);
        }
        catch (JsonException)
        {
            // Re-throw JsonException as-is for callers to handle
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to deserialize UTF-8 JSON to type {typeof(T).Name}.", ex);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This method does NOT dispose of the stream. The caller retains ownership and must dispose of it.
    /// The stream is flushed after serialization to ensure all data is written.
    /// </remarks>
    async Task IXpingSerializer.SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value), "Cannot serialize null value.");
        }

        if (!stream.CanWrite)
        {
            throw new ArgumentException("Stream must be writable.", nameof(stream));
        }

        try
        {
            await JsonSerializer.SerializeAsync(stream, value, _options, cancellationToken)
                .ConfigureAwait(false);

            // Flush to ensure all data is written to the stream
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not ArgumentNullException and not ArgumentException)
        {
            throw new InvalidOperationException($"Failed to serialize object of type {typeof(T).Name} to stream.", ex);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This method does NOT dispose of the stream. The caller retains ownership and must dispose of it.
    /// The stream position is advanced as data is read, and the stream remains open after completion.
    /// </remarks>
    async Task<T?> IXpingSerializer.DeserializeAsync<T>(
        Stream stream,
        CancellationToken cancellationToken) where T : class
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }

        try
        {
            return await JsonSerializer
                .DeserializeAsync<T>(stream, _options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // Re-throw JsonException as-is for callers to handle
            throw;
        }
        catch (Exception ex) when (ex is not ArgumentNullException and not ArgumentException)
        {
            throw new InvalidOperationException($"Failed to deserialize stream to type {typeof(T).Name}.", ex);
        }
    }
}
