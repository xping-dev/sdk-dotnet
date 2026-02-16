/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Services.Serialization;

/// <summary>
/// Defines the contract for serialization and deserialization operations in the Xping SDK.
/// This interface provides a centralized way to serialize test execution data to JSON format
/// and deserialize it back to strongly typed objects.
/// </summary>
public interface IXpingSerializer
{
    /// <summary>
    /// Serializes an object to a JSON string.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>A JSON string representation of the object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    string Serialize<T>(T value);

    /// <summary>
    /// Asynchronously serializes an object to a stream in JSON format.
    /// This method is useful for serializing large objects or when working with streams.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="stream">The stream to write the JSON to.</param>
    /// <param name="value">The object to serialize.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> or <paramref name="value"/> is null.</exception>
    /// <remarks>
    /// The stream is NOT disposed of by this method. The caller retains ownership of the stream
    /// and is responsible for disposing of it. The stream position is advanced to the end of the
    /// written content, and the stream is flushed to ensure all data is written.
    /// </remarks>
    Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes an object to a UTF-8 encoded byte array.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>A UTF-8 encoded byte array containing the JSON representation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    byte[] SerializeToUtf8Bytes<T>(T value);

    /// <summary>
    /// Deserializes a JSON string to an object of the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized object, or null if the JSON represents a null value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
    /// <exception cref="System.Text.Json.JsonException">Thrown when the JSON is invalid or cannot be converted to the target type.</exception>
    T? Deserialize<T>(string json) where T : class;

    /// <summary>
    /// Deserializes a UTF-8 encoded JSON byte array to an object of the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="utf8Json">The UTF-8 encoded JSON byte array to deserialize.</param>
    /// <returns>The deserialized object, or null if the JSON represents a null value.</returns>
    /// <exception cref="System.Text.Json.JsonException">Thrown when the JSON is invalid or cannot be converted to the target type.</exception>
    T? Deserialize<T>(ReadOnlySpan<byte> utf8Json) where T : class;

    /// <summary>
    /// Asynchronously deserializes a stream containing JSON to an object of the specified type.
    /// This method is useful for deserializing large objects or when working with streams.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="stream">The stream containing the JSON to deserialize.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation, containing the deserialized object or null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.Text.Json.JsonException">Thrown when the JSON is invalid or cannot be converted to the target type.</exception>
    /// <remarks>
    /// The stream is NOT disposed of by this method. The caller retains ownership of the stream
    /// and is responsible for disposing of it. The stream position is advanced as data is read.
    /// The stream remains open after deserialization completes.
    /// </remarks>
    Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : class;
}
