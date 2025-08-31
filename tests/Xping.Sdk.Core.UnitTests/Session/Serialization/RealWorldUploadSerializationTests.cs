using System;
using System.IO;
using NUnit.Framework;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Core.Session.Serialization;

namespace Xping.Sdk.Core.UnitTests.Session.Serialization;

[TestFixture]
internal class RealWorldUploadSerializationTests
{
    [Test]
    public void TestSessionAfterUploadScenario()
    {
        // Simulate real upload scenario
        using var session = new TestSession
        {
            Url = new Uri("https://example.com"),
            StartDate = DateTime.UtcNow,
            State = TestSessionState.Completed,
            Steps = []
        ,
            TestSettings = new TestSettings()
        };

        // Initially should not be uploaded
        Assert.That(session.UploadedAt, Is.Null);
        Assert.That(session.IsUploaded, Is.False);

        // Serialize before upload - should not contain UploadedAt
        var serializer = new TestSessionSerializer();
        using var streamBefore = new MemoryStream();
        serializer.Serialize(session, streamBefore, SerializationFormat.XML);
        
        streamBefore.Position = 0;
        string xmlBeforeUpload;
        using (var reader = new StreamReader(streamBefore, leaveOpen: true))
        {
            xmlBeforeUpload = reader.ReadToEnd();
        }
        
        // Check that UploadedAt element is present but nil
        Assert.That(xmlBeforeUpload.Contains("UploadedAt", StringComparison.Ordinal), Is.True, "UploadedAt element should be present even when null");
        Assert.That(xmlBeforeUpload.Contains("i:nil=\"true\"", StringComparison.Ordinal), Is.True, "UploadedAt should be marked as nil when null");

        // Simulate successful upload
        var uploadedAt = DateTime.UtcNow;
        session.MarkAsUploaded(uploadedAt);

        Assert.That(session.UploadedAt, Is.EqualTo(uploadedAt));
        Assert.That(session.IsUploaded, Is.True);

        // Serialize after upload - should contain UploadedAt value
        using var streamAfter = new MemoryStream();
        serializer.Serialize(session, streamAfter, SerializationFormat.XML);
        
        streamAfter.Position = 0;
        string xmlAfterUpload;
        using (var reader = new StreamReader(streamAfter, leaveOpen: true))
        {
            xmlAfterUpload = reader.ReadToEnd();
        }

        // Save for inspection
        File.WriteAllText("/tmp/before_upload.xml", xmlBeforeUpload);
        File.WriteAllText("/tmp/after_upload.xml", xmlAfterUpload);

        // Verify UploadedAt is properly serialized after upload
        Assert.That(xmlAfterUpload.Contains("UploadedAt", StringComparison.Ordinal), Is.True, "UploadedAt element should be present");
        Assert.That(xmlAfterUpload.Contains("<UploadedAt i:nil=\"true\"", StringComparison.Ordinal), Is.False, "UploadedAt should not be nil after upload");
        Assert.That(xmlAfterUpload.Contains("UploadedAt i:type=\"x:dateTime\"", StringComparison.Ordinal), Is.True, "UploadedAt should have correct type after upload");
        
        // Test deserialization preserves upload state
        streamAfter.Position = 0;
        var deserializedSession = serializer.Deserialize(streamAfter, SerializationFormat.XML);
        
        Assert.That(deserializedSession?.UploadedAt, Is.EqualTo(uploadedAt));
        Assert.That(deserializedSession?.IsUploaded, Is.True);
    }
}