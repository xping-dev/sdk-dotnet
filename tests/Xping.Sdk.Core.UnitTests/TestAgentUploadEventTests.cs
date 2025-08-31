/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Core.Session.Collector;
using Xping.Sdk.Core.DependencyInjection;
using System.Net;

namespace Xping.Sdk.Core.UnitTests;

[TestFixture]
public sealed class TestAgentUploadEventTests
{
    private IServiceProvider _serviceProvider = null!;
    private Mock<ITestSessionUploader> _mockUploader = null!;

    [SetUp]
    public void Setup()
    {
        _mockUploader = new Mock<ITestSessionUploader>();
        
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(implementationInstance: Mock.Of<IProgress<TestStep>>());
            services.AddSingleton(_mockUploader.Object);
            services.AddTestAgent();
        });

        var host = builder.Build();
        _serviceProvider = host.Services;
    }

    [Test]
    public async Task RunAsyncWhenUploadSucceedsRaisesUploadSucceededEvent()
    {
        // Arrange
        using var testAgent = new TestAgent(_serviceProvider);
        testAgent.UploadToken = Guid.NewGuid().ToString();
        
        var testUrl = new Uri("http://test.example.com");
        var uploadResult = UploadResult.Success(HttpStatusCode.OK);
        
        _mockUploader
            .Setup(x => x.UploadAsync(It.IsAny<TestSession>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadResult)
            .Callback<TestSession, string?, CancellationToken>((session, apiKey, ct) =>
            {
                // Simulate what the real uploader does on success
                session.MarkAsUploaded(DateTime.UtcNow);
            });

        UploadSuccessEventArgs? capturedEventArgs = null;
        testAgent.UploadSucceeded += (sender, e) => capturedEventArgs = e;

        // Act
        var testSession = await testAgent.RunAsync(testUrl).ConfigureAwait(false);

        // Assert
        Assert.That(capturedEventArgs, Is.Not.Null, "UploadSucceeded event should have been raised");
        Assert.That(capturedEventArgs.Session, Is.EqualTo(testSession), "Event args should contain the uploaded test session");
        Assert.That(capturedEventArgs.UploadToken, Is.EqualTo(testSession.UploadToken), "Event args should contain the upload token");
        Assert.That(capturedEventArgs.UploadedAt, Is.EqualTo(testSession.UploadedAt!.Value), "Event args should contain the uploaded timestamp");
        Assert.That(testSession.IsUploaded, Is.True, "Test session should be marked as uploaded");
    }

    [Test]
    public async Task RunAsyncWhenUploadFailsDoesNotRaiseUploadSucceededEvent()
    {
        // Arrange
        using var testAgent = new TestAgent(_serviceProvider);
        testAgent.UploadToken = Guid.NewGuid().ToString();
        
        var testUrl = new Uri("http://test.example.com");
        var uploadResult = UploadResult.Failure("Upload failed", statusCode: HttpStatusCode.InternalServerError);
        
        _mockUploader
            .Setup(x => x.UploadAsync(It.IsAny<TestSession>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadResult);

        var uploadSucceededRaised = false;
        testAgent.UploadSucceeded += (sender, e) => uploadSucceededRaised = true;

        // Act
        var testSession = await testAgent.RunAsync(testUrl).ConfigureAwait(false);

        // Assert
        Assert.That(uploadSucceededRaised, Is.False, "UploadSucceeded event should not have been raised for failed upload");
        Assert.That(testSession.IsUploaded, Is.False, "Test session should not be marked as uploaded");
    }

    [Test]
    public async Task RunAsyncWithoutUploadTokenDoesNotRaiseUploadSucceededEvent()
    {
        // Arrange
        using var testAgent = new TestAgent(_serviceProvider);
        // No upload token set
        
        var testUrl = new Uri("http://test.example.com");

        var uploadSucceededRaised = false;
        testAgent.UploadSucceeded += (sender, e) => uploadSucceededRaised = true;

        // Act
        var testSession = await testAgent.RunAsync(testUrl).ConfigureAwait(false);

        // Assert
        Assert.That(uploadSucceededRaised, Is.False, "UploadSucceeded event should not have been raised when no upload token is set");
        Assert.That(testSession.IsUploaded, Is.False, "Test session should not be marked as uploaded");
        
        // Verify that upload was never called
        _mockUploader.Verify(x => x.UploadAsync(It.IsAny<TestSession>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void UploadSuccessEventArgsConstructorSetsPropertiesCorrectly()
    {
        // Arrange
        using var testSession = new TestSession
        {
            Url = new Uri("http://test.example.com"),
            StartDate = DateTime.UtcNow,
            Steps = [],
            State = TestSessionState.Completed
        ,
            TestSettings = new TestSettings()
        };
        var uploadedAt = DateTime.UtcNow;
        var uploadToken = Guid.NewGuid();

        // Act
        var eventArgs = new UploadSuccessEventArgs(testSession, uploadedAt, uploadToken);

        // Assert
        Assert.That(eventArgs.Session, Is.EqualTo(testSession));
        Assert.That(eventArgs.UploadedAt, Is.EqualTo(uploadedAt));
        Assert.That(eventArgs.UploadToken, Is.EqualTo(uploadToken));
    }

    [Test]
    public void UploadSuccessEventArgsConstructorThrowsArgumentNullExceptionWhenSessionIsNull()
    {
        // Arrange
        var uploadedAt = DateTime.UtcNow;
        var uploadToken = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UploadSuccessEventArgs(null!, uploadedAt, uploadToken));
    }
}