/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using Xping.Sdk.Core.Common;

namespace Xping.Sdk.Core.Session.Serialization;

/// <summary>
/// A class that provides serialization and deserialization of test sessions
/// </summary>
/// <remarks>
/// This class supports two formats: binary and XML. Users can choose the format that suits their needs and preferences, 
/// depending on the size, readability, and compatibility of the data.
/// </remarks>
public sealed class TestSessionSerializer : ITestSessionSerializer
{
    private readonly DataContractSerializer _dataContractSerializer = new(
        type: typeof(TestSession),
        knownTypes: GetKnownTypes());

    /// <summary>
    /// Serializes a test session to a stream using the specified format.
    /// </summary>
    /// <param name="session">The test session to serialize</param>
    /// <param name="stream">The stream to save the serialized data</param>
    /// <param name="format">The format to use for serialization: Binary or XML</param>
    /// <param name="ownsStream">
    /// True to indicate that the writer closes the stream when done, otherwise false
    /// </param>
    public void Serialize(TestSession session, Stream stream, SerializationFormat format, bool ownsStream = false)
    {
        ArgumentNullException.ThrowIfNull(session, nameof(session));
        ArgumentNullException.ThrowIfNull(stream, nameof(stream));

        using var writer = format switch
        {
            SerializationFormat.Binary => XmlDictionaryWriter.CreateBinaryWriter(
                stream, dictionary: null, session: null, ownsStream: false),
            SerializationFormat.XML => XmlDictionaryWriter.CreateTextWriter(stream, Encoding.UTF8, ownsStream),
            _ => throw new NotSupportedException("Only Binary or XML is supported as serialization format.")
        };

        _dataContractSerializer.WriteObject(writer, session);

        // Flush the writer to ensure that all data is written to the stream
        writer.Flush();
        // Reset the stream position to 0
        stream.Position = 0;
    }

    /// <summary>
    /// Deserializes a test session from a stream using the specified format.
    /// </summary>
    /// <param name="stream">The stream to load the serialized data</param>
    /// <param name="format">The format to use for deserialization: Binary or XML</param>
    /// <returns>The deserialized test session</returns>
    /// <exception cref="SerializationException">When incorrect serialization format or data</exception>
    public TestSession? Deserialize(Stream stream, SerializationFormat format)
    {
        ArgumentNullException.ThrowIfNull(stream, nameof(stream));

        using var reader = format switch
        {
            SerializationFormat.Binary => XmlDictionaryReader.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max),
            SerializationFormat.XML => XmlDictionaryReader.CreateTextReader(stream, XmlDictionaryReaderQuotas.Max),
            _ => throw new NotSupportedException("Only Binary or XML is supported as serialization format.")
        };

        var result = _dataContractSerializer.ReadObject(reader, true);

        return result as TestSession;
    }

    private static List<Type> GetKnownTypes() =>
    [
        typeof(TestStep[]),
        typeof(TestMetadata),
        typeof(PropertyBag<IPropertyBagValue>),
        typeof(Dictionary<PropertyBagKey, IPropertyBagValue>),
        typeof(PropertyBagValue<byte[]>),
        typeof(PropertyBagValue<string>),
        typeof(PropertyBagValue<string[]>),
        typeof(PropertyBagValue<Dictionary<string, string>>),
        typeof(PropertyBagValue<int>),
        typeof(PropertyBagValue<double>),
        typeof(PropertyBagValue<bool>),
        typeof(PropertyBagValue<DateTime>),
        typeof(PropertyBagValue<TimeSpan>)
    ];
}