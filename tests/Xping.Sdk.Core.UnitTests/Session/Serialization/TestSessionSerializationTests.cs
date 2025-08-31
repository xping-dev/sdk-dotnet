/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Globalization;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Core.Session.Serialization;

namespace Xping.Sdk.Core.UnitTests.Session.Serialization;

internal class TestSessionSerializationTests
{
    [Test]
    public void SerializeThrowsArgumentNullExceptionWhenTestSessionIsNull()
    {
        // Arrange
        TestSession session = null!;
        var serializer = new TestSessionSerializer();

        // Assert
        Assert.Throws<ArgumentNullException>(() => serializer.Serialize(session, Stream.Null, SerializationFormat.XML));
    }

    [Test]
    public void SerializeThrowsArgumentNullExceptionWhenStreamIsNull()
    {
        // Arrange
        using TestSession session = new()
        {
            Url = new Uri("https://test"),
            StartDate = DateTime.UtcNow,
            State = TestSessionState.NotStarted,
            Steps = [],
            TestSettings = new TestSettings()
        };
        var serializer = new TestSessionSerializer();

        // Assert
        Assert.Throws<ArgumentNullException>(() =>
            serializer.Serialize(
                session,
                stream: null!,
                SerializationFormat.XML));
    }

    [Test]
    public void DeserializeThrowsArgumentNullExceptionWhenStreamIsNull()
    {
        // Arrange
        using TestSession session = new()
        {
            Url = new Uri("https://test"),
            StartDate = DateTime.UtcNow,
            State = TestSessionState.NotStarted,
            Steps = [],
            TestSettings = new TestSettings()
        };
        var serializer = new TestSessionSerializer();

        // Assert
        Assert.Throws<ArgumentNullException>(() =>
            serializer.Deserialize(
                stream: null!,
                SerializationFormat.XML));
    }

    [Test]
    public void TestSessionIdShouldBeSerialized()
    {
        // Arrange
        using TestSession session = new()
        {
            Url = new Uri("https://test"),
            StartDate = DateTime.UtcNow,
            State = TestSessionState.NotStarted,
            Steps = [],
            TestSettings = new TestSettings()
        };
        var serializer = new TestSessionSerializer();
        using var stream = new MemoryStream();
        
        // Act
        serializer.Serialize(session, stream, SerializationFormat.XML);
        stream.Position = 0;

        using var reader = new StreamReader(stream, Encoding.UTF8);

        // Load the XML into an XmlDocument
        var doc = new XmlDocument();
        doc.Load(reader);
        
        // Create a NamespaceManager and add the namespace used in the XML
        var namespaceManager = new XmlNamespaceManager(doc.NameTable);
        namespaceManager.AddNamespace("ns", "http://schemas.datacontract.org/2004/07/Xping.Sdk.Core.Session");

        // Add the namespace prefix in the XPath expression to select the Id element
        XmlNode? idNode = doc.SelectSingleNode("//ns:TestSession/Id", namespaceManager);

        // Assert
        Assert.That(idNode, Is.Not.Null);
        Assert.That(Guid.TryParse(idNode.InnerText, out _), Is.True);
        Assert.That(Guid.Parse(idNode.InnerText), Is.EqualTo(session.Id));
    }

    [Test]
    public void TestComponentIterationShouldBeSerialized()
    {
        // Arrange
        const int expectedCount = 1;

        using TestSession session = new()
        {
            Url = new Uri("https://test"),
            StartDate = DateTime.UtcNow,
            State = TestSessionState.NotStarted,
            Steps = [new TestStep
            {
                Duration = TimeSpan.Zero,
                Name = "stepName",
                Description = "Test step description",
                DisplayName = "Step Display Name",
                PropertyBag = null,
                Result = TestStepResult.Succeeded,
                StartDate = DateTime.UtcNow,
                TestComponentIteration = expectedCount,
                Type = TestStepType.ActionStep
            }],
            TestSettings = new TestSettings()
        };
        var serializer = new TestSessionSerializer();
        using var stream = new MemoryStream();

        // Act
        serializer.Serialize(session, stream, SerializationFormat.XML);
        stream.Position = 0;

        using var reader = new StreamReader(stream, Encoding.UTF8);

        // Load the XML into an XmlDocument
        var doc = new XmlDocument();
        doc.Load(reader);

        // Create a NamespaceManager and add the namespace used in the XML
        var namespaceManager = new XmlNamespaceManager(doc.NameTable);
        namespaceManager.AddNamespace("ns", "http://schemas.datacontract.org/2004/07/Xping.Sdk.Core.Session");

        // Add the namespace prefix in the XPath expression to select the Id element
        XmlNode? node = doc.SelectSingleNode("//ns:TestStep/TestComponentIteration", namespaceManager);

        // Assert
        Assert.That(node, Is.Not.Null);
        Assert.That(node.InnerText, Is.EqualTo(expectedCount.ToString(CultureInfo.InvariantCulture)));
    }

    [Test]
    public void TestSessionInstanceIsCorrectlySerializedToXml()
    {
        // Arrange
        using TestSession session = new()
        {
            Url = new Uri("https://test"),
            StartDate = DateTime.UtcNow,
            State = TestSessionState.NotStarted,
            Steps = [new TestStep
            {
                Name = "step1",
                Description = "Test step description",
                DisplayName = "Step Display Name",
                TestComponentIteration = 1,
                Duration = TimeSpan.Zero,
                PropertyBag = new PropertyBag<IPropertyBagValue>(),
                Result = TestStepResult.Failed,
                StartDate = DateTime.Now,
                Type = TestStepType.ActionStep
            }],
            TestSettings = new TestSettings()
        };

        var serializer = new TestSessionSerializer();
        using var stream = new MemoryStream();

        // Act
        serializer.Serialize(session, stream, SerializationFormat.XML);
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8);

        // Load the XML into an XmlDocument
        var doc = new XmlDocument();
        doc.Load(reader);

        // Create a NamespaceManager and add the namespace used in the XML
        var namespaceManager = new XmlNamespaceManager(doc.NameTable);
        namespaceManager.AddNamespace("ns", "http://schemas.datacontract.org/2004/07/Xping.Sdk.Core.Session");

        // Add the namespace prefix in the XPath expression to select the Url element
        XmlNode? urlNode = doc.SelectSingleNode("//ns:TestSession/Url", namespaceManager);

        // Assert
        Assert.That(urlNode, Is.Not.Null);
        Assert.That(Uri.TryCreate(urlNode.InnerText, UriKind.Absolute, out _), Is.True);
        Assert.That(new Uri(urlNode.InnerText), Is.EqualTo(session.Url));
    }

    [TestCase(SerializationFormat.XML)]
    [TestCase(SerializationFormat.Binary)]
    [Test]
    public void TestSessionInstanceIsCorrectlyDeserialized(SerializationFormat format)
    {
        // Arrange
        using TestSession session = new()
        {
            Url = new Uri("https://test"),
            StartDate = DateTime.UtcNow,
            State = TestSessionState.NotStarted,
            Steps = [new TestStep
            {
                Name = "step1",
                Description = "Test step description",
                DisplayName = "Step Display Name",
                TestComponentIteration = 1,
                Duration = TimeSpan.Zero,
                PropertyBag = new PropertyBag<IPropertyBagValue>(),
                Result = TestStepResult.Failed,
                StartDate = DateTime.Now,
                Type = TestStepType.ActionStep
            }],
            TestSettings = new TestSettings()
        };

        var serializer = new TestSessionSerializer();
        using var stream = new MemoryStream();

        // Act
        serializer.Serialize(session, stream, format);
        stream.Position = 0;
        TestSession? session1 = serializer.Deserialize(stream, format);

        // Assert
        Assert.That(session1, Is.Not.Null);
        Assert.That(session1.Url.AbsoluteUri, Is.EqualTo(session.Url.AbsoluteUri));
        Assert.That(session1.StartDate, Is.EqualTo(session.StartDate));
        Assert.That(session1.Steps.Count, Is.EqualTo(session.Steps.Count));
        Assert.That(session1.Steps.First().Name, Is.EqualTo(session.Steps.First().Name));
        Assert.That(session1.Steps.First().TestComponentIteration, Is.EqualTo(session.Steps.First().TestComponentIteration));
        Assert.That(session1.Steps.First().Duration, Is.EqualTo(session.Steps.First().Duration));
        Assert.That(session1.Steps.First().PropertyBag, Is.Not.Null);
        Assert.That(session1.Steps.First().StartDate, Is.EqualTo(session.Steps.First().StartDate));
        Assert.That(session1.Steps.First().Type, Is.EqualTo(session.Steps.First().Type));
    }

    [TestCase(SerializationFormat.XML)]
    [TestCase(SerializationFormat.Binary)]
    [Test]
    public void TestSessionIdIsCorrectlyDeserialized(SerializationFormat format)
    {
        // Arrange
        using TestSession session = new()
        {
            Url = new Uri("https://test"),
            StartDate = DateTime.UtcNow,
            State = TestSessionState.NotStarted,
            Steps = [],
            TestSettings = new TestSettings()
        };

        var serializer = new TestSessionSerializer();
        using var stream = new MemoryStream();

        // Act
        serializer.Serialize(session, stream, format);
        stream.Position = 0;
        TestSession? session1 = serializer.Deserialize(stream, format);

        // Assert
        Assert.That(session1, Is.Not.Null);
        Assert.That(session.Id, Is.EqualTo(session1.Id));
    }

    [Test]
    public void DeserializerThrowsSerializationExceptionWhenIncorrectSerializationFormatProvided()
    {
        // Arrange
        using TestSession session = new()
        {
            Url = new Uri("https://test"),
            StartDate = DateTime.UtcNow,
            State = TestSessionState.NotStarted,
            Steps = [new TestStep
            {
                Name = "step1",
                Description = "Test step description",
                DisplayName = "Step Display Name",
                TestComponentIteration = 1,
                Duration = TimeSpan.Zero,
                PropertyBag = new PropertyBag<IPropertyBagValue>(),
                Result = TestStepResult.Failed,
                StartDate = DateTime.Now,
                Type = TestStepType.ActionStep
            }],
            TestSettings = new TestSettings()
        };

        var serializer = new TestSessionSerializer();
        using var stream = new MemoryStream();

        // Act
        serializer.Serialize(session, stream, SerializationFormat.XML);
        stream.Position = 0;

        Assert.Throws<SerializationException>(() => serializer.Deserialize(stream, SerializationFormat.Binary));
    }

    [Test]
    public void TestSessionUploadedAtShouldBeSerializedToXml()
    {
        // Arrange
        using TestSession session = new()
        {
            Url = new Uri("https://test.example.com"),
            StartDate = DateTime.UtcNow,
            State = TestSessionState.Completed,
            Steps = [],
            TestSettings = new TestSettings()
        };

        var uploadedAt = DateTime.UtcNow;
        session.MarkAsUploaded(uploadedAt);

        var serializer = new TestSessionSerializer();
        using var stream = new MemoryStream();

        // Act
        serializer.Serialize(session, stream, SerializationFormat.XML);
        
        // Get XML content for inspection
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var xmlContent = reader.ReadToEnd();

        // Reset stream for deserialization
        stream.Position = 0;
        var deserializedSession = serializer.Deserialize(stream, SerializationFormat.XML);

        // Assert
        Assert.That(session.UploadedAt, Is.EqualTo(uploadedAt), "Original session should have UploadedAt set");
        Assert.That(xmlContent.Contains("UploadedAt", StringComparison.Ordinal), Is.True, "XML should contain UploadedAt element");
        Assert.That(deserializedSession?.UploadedAt, Is.EqualTo(uploadedAt), "Deserialized session should preserve UploadedAt");
        Assert.That(deserializedSession?.IsUploaded, Is.True, "Deserialized session should be marked as uploaded");
    }

    [Test]
    public void TestSessionWithoutUploadedAtShouldHaveNullUploadedAtInXml()
    {
        // Arrange
        using TestSession session = new()
        {
            Url = new Uri("https://test.example.com"),
            StartDate = DateTime.UtcNow,
            State = TestSessionState.Completed,
            Steps = [],
            TestSettings = new TestSettings()
        };

        // Don't set UploadedAt (should be null)

        var serializer = new TestSessionSerializer();
        using var stream = new MemoryStream();

        // Act
        serializer.Serialize(session, stream, SerializationFormat.XML);
        
        // Get XML content for inspection
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var xmlContent = reader.ReadToEnd();

        // Reset stream for deserialization
        stream.Position = 0;
        var deserializedSession = serializer.Deserialize(stream, SerializationFormat.XML);

        // Assert
        Assert.That(session.UploadedAt, Is.Null, "Original session should have UploadedAt as null");
        Assert.That(deserializedSession?.UploadedAt, Is.Null, "Deserialized session should preserve null UploadedAt");
        Assert.That(deserializedSession?.IsUploaded, Is.False, "Deserialized session should not be marked as uploaded");
    }
}
