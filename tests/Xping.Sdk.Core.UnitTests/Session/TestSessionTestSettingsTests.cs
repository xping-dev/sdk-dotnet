/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Core.Session.Serialization;

namespace Xping.Sdk.UnitTests.Session;

/// <summary>
/// Tests for TestSession TestSettings functionality including serialization and defaults.
/// </summary>
public sealed class TestSessionTestSettingsTests
{
    [Test]
    public void TestSessionAlwaysHasTestSettings()
    {
        // Arrange & Act
        using var session = new TestSession
        {
            Url = new Uri("https://test.example.com"),
            StartDate = DateTime.UtcNow,
            Steps = [],
            State = TestSessionState.NotStarted,
            TestSettings = new TestSettings()
        };

        // Assert
        Assert.That(session.TestSettings, Is.Not.Null, "TestSettings should never be null");
    }

    [Test]
    public void TestSessionWithTestSettingsHasCorrectDefaults()
    {
        // Arrange & Act
        using var session = new TestSession
        {
            Url = new Uri("https://test.example.com"),
            StartDate = DateTime.UtcNow,
            Steps = [],
            State = TestSessionState.NotStarted,
            TestSettings = new TestSettings()
        };

        // Assert
        Assert.That(session.TestSettings, Is.Not.Null);
        Assert.That(session.TestSettings.ContinueOnFailure, Is.False, "ContinueOnFailure should default to false");
#if DEBUG
        Assert.That(session.TestSettings.Timeout, Is.EqualTo(TimeSpan.FromMinutes(30)), "Timeout should default to 30 minutes in DEBUG");
#else
        Assert.That(session.TestSettings.Timeout, Is.EqualTo(TimeSpan.FromSeconds(30)), "Timeout should default to 30 seconds in RELEASE");
#endif
    }

    [Test]
    public void TestSessionWithCustomTestSettingsRetainsValues()
    {
        // Arrange
        var customSettings = new TestSettings
        {
            ContinueOnFailure = true,
            Timeout = TimeSpan.FromMinutes(5)
        };

        // Act
        using var session = new TestSession
        {
            Url = new Uri("https://test.example.com"),
            StartDate = DateTime.UtcNow,
            Steps = [],
            State = TestSessionState.NotStarted,
            TestSettings = customSettings
        };

        // Assert
        Assert.That(session.TestSettings, Is.Not.Null);
        Assert.That(session.TestSettings.ContinueOnFailure, Is.True);
        Assert.That(session.TestSettings.Timeout, Is.EqualTo(TimeSpan.FromMinutes(5)));
    }

    [Test]
    public void TestSettingsSerializesToXmlCorrectly()
    {
        // Arrange
        var customSettings = new TestSettings
        {
            ContinueOnFailure = true,
            Timeout = TimeSpan.FromMinutes(10)
        };

        using var session = new TestSession
        {
            Url = new Uri("https://test.example.com"),
            StartDate = DateTime.UtcNow,
            Steps = [],
            State = TestSessionState.NotStarted,
            TestSettings = customSettings
        };

        var serializer = new TestSessionSerializer();
        using var stream = new MemoryStream();

        // Act
        serializer.Serialize(session, stream, SerializationFormat.XML);
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        var xml = reader.ReadToEnd();

        // Assert
        Assert.That(xml, Contains.Substring("TestSettings"));
        Assert.That(xml, Contains.Substring(">true<"), "ContinueOnFailure should be true");
        Assert.That(xml, Contains.Substring(">PT10M<"), "Timeout should be PT10M (10 minutes)");
    }

    [Test]
    public void TestSettingsDeserializesFromXmlCorrectly()
    {
        // Arrange
        var originalSettings = new TestSettings
        {
            ContinueOnFailure = true,
            Timeout = TimeSpan.FromMinutes(15)
        };

        using var originalSession = new TestSession
        {
            Url = new Uri("https://test.example.com"),
            StartDate = DateTime.UtcNow,
            Steps = [],
            State = TestSessionState.NotStarted,
            TestSettings = originalSettings
        };

        var serializer = new TestSessionSerializer();
        using var stream = new MemoryStream();

        serializer.Serialize(originalSession, stream, SerializationFormat.XML);
        stream.Position = 0;

        // Act
        var deserializedSession = serializer.Deserialize(stream, SerializationFormat.XML);

        // Assert
        Assert.That(deserializedSession, Is.Not.Null);
        Assert.That(deserializedSession.TestSettings, Is.Not.Null);
        Assert.That(deserializedSession.TestSettings.ContinueOnFailure, Is.True);
        Assert.That(deserializedSession.TestSettings.Timeout, Is.EqualTo(TimeSpan.FromMinutes(15)));
    }

    [Test]
    public void TestSettingsSerializesToBinaryCorrectly()
    {
        // Arrange
        var customSettings = new TestSettings
        {
            ContinueOnFailure = false,
            Timeout = TimeSpan.FromSeconds(45)
        };

        using var session = new TestSession
        {
            Url = new Uri("https://test.example.com"),
            StartDate = DateTime.UtcNow,
            Steps = [],
            State = TestSessionState.NotStarted,
            TestSettings = customSettings
        };

        var serializer = new TestSessionSerializer();
        using var stream = new MemoryStream();

        // Act
        serializer.Serialize(session, stream, SerializationFormat.Binary);
        stream.Position = 0;

        var deserializedSession = serializer.Deserialize(stream, SerializationFormat.Binary);

        // Assert
        Assert.That(deserializedSession, Is.Not.Null);
        Assert.That(deserializedSession.TestSettings, Is.Not.Null);
        Assert.That(deserializedSession.TestSettings.ContinueOnFailure, Is.False);
        Assert.That(deserializedSession.TestSettings.Timeout, Is.EqualTo(TimeSpan.FromSeconds(45)));
    }

    [Test]
    public void TestSessionWithoutTestSettingsInSerializedDataUsesDefaults()
    {
        // This test simulates deserializing old TestSession data that doesn't have TestSettings
        // by manually creating serialization info without TestSettings

        // Arrange
        using var session = new TestSession
        {
            Url = new Uri("https://test.example.com"),
            StartDate = DateTime.UtcNow,
            Steps = [],
            State = TestSessionState.NotStarted,
            TestSettings = new TestSettings()
        };

        // Serialize with full data
        var serializer = new TestSessionSerializer();
        using var stream = new MemoryStream();
        serializer.Serialize(session, stream, SerializationFormat.XML);
        stream.Position = 0;

        // Read and modify XML to remove TestSettings element (simulating old data)
        using var reader = new StreamReader(stream);
        var xml = reader.ReadToEnd();
        
        // Remove TestSettings from XML to simulate old data format
        var startTag = "<TestSettings>";
        var endTag = "</TestSettings>";
        var startIndex = xml.IndexOf(startTag);
        var endIndex = xml.IndexOf(endTag) + endTag.Length;
        
        if (startIndex >= 0 && endIndex >= 0)
        {
            xml = xml.Remove(startIndex, endIndex - startIndex);
        }

        // Act - Try to deserialize modified XML
        using var modifiedStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        var deserializedSession = serializer.Deserialize(modifiedStream, SerializationFormat.XML);

        // Assert - Should still work and use defaults
        Assert.That(deserializedSession, Is.Not.Null);
        Assert.That(deserializedSession.TestSettings, Is.Not.Null);
        Assert.That(deserializedSession.TestSettings.ContinueOnFailure, Is.False);
#if DEBUG
        Assert.That(deserializedSession.TestSettings.Timeout, Is.EqualTo(TimeSpan.FromMinutes(30)));
#else
        Assert.That(deserializedSession.TestSettings.Timeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
#endif
    }
}