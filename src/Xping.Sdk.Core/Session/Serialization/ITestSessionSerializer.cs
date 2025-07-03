/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Serialization;

/// <summary>
/// Interface for test session serialization and deserialization
/// </summary>
/// <remarks>
/// This interface supports two formats: binary and XML. Users can choose the format that suits their needs and preferences, 
/// depending on the size, readability, and compatibility of the data.
/// </remarks>
public interface ITestSessionSerializer
{
    /// <summary>
    /// Serializes a test session to a stream using the specified format.
    /// </summary>
    /// <param name="session">The test session to serialize</param>
    /// <param name="stream">The stream to save the serialized data</param>
    /// <param name="format">The format to use for serialization: Binary or XML</param>
    /// <param name="ownsStream">True to indicate that the writer closes the stream when done, otherwise false</param>
    void Serialize(TestSession session, Stream stream, SerializationFormat format, bool ownsStream = false);

    /// <summary>
    /// Deserializes a test session from a stream using the specified format.
    /// </summary>
    /// <param name="stream">The stream to load the serialized data</param>
    /// <param name="format">The format to use for deserialization: Binary or XML</param>
    /// <returns>The deserialized test session or null if deserialization fails</returns>
    TestSession? Deserialize(Stream stream, SerializationFormat format);
}