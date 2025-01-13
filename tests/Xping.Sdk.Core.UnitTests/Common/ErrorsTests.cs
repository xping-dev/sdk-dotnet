/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.ComponentModel;
using System.Net;
using System.Threading;
using Microsoft.AspNetCore.Components;
using Moq;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.UnitTests.Common;

public sealed class ErrorsTests
{
    private Mock<ITestComponent> _mockTestComponent;
    private readonly string _testComponentName = "Test";

    [SetUp]
    public void SetUp()
    {
        _mockTestComponent = new Mock<ITestComponent>();
        _mockTestComponent.SetupGet(m => m.Name).Returns(_testComponentName);
    }

    [Test]
    public void ExceptionError()
    {
        // Arrange
        Exception ex = new ArgumentException(message: "foo");

        // Act
        var error = Errors.ExceptionError(ex);

        // Assert
        Assert.That(error.Code, Is.EqualTo("1000"));
        Assert.That(error.Message, Is.EqualTo("Message: foo"));
    }

    [Test]
    public void HttpClientsNotFound()
    {
        // Act
        var error = Errors.HttpClientsNotFound;

        // Assert
        Assert.That(error.Code, Is.EqualTo("1010"));
        Assert.That(error.Message, Is.EqualTo(
            "The service provider does not have any Http clients registered. " +
            "You need to invoke `AddHttpClients()` to add them before you can use them."));
    }

    [Test]
    public void HeadlessBrowserNotFound()
    {
        // Act
        var error = Errors.HeadlessBrowserNotFound;

        // Assert
        Assert.That(error.Code, Is.EqualTo("1011"));
        Assert.That(error.Message, Is.EqualTo(
            "The service provider does not have any Headless browsers registered. You need to invoke " +
            "`AddBrowserClients()` to add them before you can use them."));
    }

    [Test]
    public void IncorrectClientType()
    {
        // Act
        var error = Errors.IncorrectClientType;

        // Assert
        Assert.That(error.Code, Is.EqualTo("1012"));
        Assert.That(error.Message, Is.EqualTo(
            "The client type is not supported. Please use either HttpClient or HeadlessBrowser."));
    }

    [Test]
    public void InsufficientData()
    {
        // Act
        var error = Errors.InsufficientData(_mockTestComponent.Object);

        // Assert
        Assert.That(error.Code, Is.EqualTo("1100"));
        Assert.That(error.Message, Is.EqualTo($"Insufficient data to perform \"{_testComponentName}\" test step."));
    }

    [Test]
    public void ValidationFailedWhenErrorMessageIsNull()
    {
        // Act
        var error = Errors.ValidationFailed(_mockTestComponent.Object, errorMessage: null);

        // Assert
        Assert.That(error.Code, Is.EqualTo("1101"));
        Assert.That(error.Message, Is.EqualTo($"Validation failed to perform \"{_testComponentName}\" test step."));
    }

    [Test]
    public void ValidationFailedWhenErrorMessageIsProvided()
    {
        // Act
        var error = Errors.ValidationFailed(_mockTestComponent.Object, errorMessage: "ErrorMessage");

        // Assert
        Assert.That(error.Code, Is.EqualTo("1101"));
        Assert.That(error.Message, Is.EqualTo(
            $"Validation failed to perform \"{_testComponentName}\" test step. ErrorMessage"));
    }

    [Test]
    public void TestComponentTimeout()
    {
        // Arrange 
        TimeSpan timeout = TimeSpan.FromSeconds(10);

        // Act
        var error = Errors.TestComponentTimeout(_mockTestComponent.Object, timeout);

        // Assert
        Assert.That(error.Code, Is.EqualTo("1102"));
        Assert.That(error.Message, Is.EqualTo(
            $"Test Operation Timeout: The test component {_testComponentName} " +
            $"exceeded the maximum allowed execution time of {timeout.GetFormattedTime(TimeSpanUnit.Second)} and has " +
            $"been marked as failed. To accommodate longer test durations, consider increasing the timeout setting " +
            $"for the test operation."));
    }

    [Test]
    public void DnsLookupFailed()
    {
        // Act
        var error = Errors.DnsLookupFailed;

        // Assert
        Assert.That(error.Code, Is.EqualTo("1110"));
        Assert.That(error.Message, Is.EqualTo($"Could not resolve the hostname to any IP address."));
    }

    [Test]
    public void PingRequestFailed()
    {
        // Act
        var error = Errors.PingRequestFailed;

        // Assert
        Assert.That(error.Code, Is.EqualTo("1111"));
        Assert.That(error.Message, Is.EqualTo($"An error occurred while sending the ping request."));
    }

    [Test]
    public void HttpStatusValidationFailed()
    {
        // Arrange
        HttpStatusCode code = HttpStatusCode.OK;

        // Act
        var error = Errors.HttpStatusValidationFailed(expectedCode: code);

        // Assert
        Assert.That(error.Code, Is.EqualTo("1112"));
        Assert.That(error.Message, Is.EqualTo(
            $"The HTTP response status code did not match the expected '{code}' code."));
    }

    [Test]
    public void NoTestStepHandlers()
    {
        // Act
        var error = Errors.NoTestStepHandlers;

        // Assert
        Assert.That(error.Code, Is.EqualTo("1200"));
        Assert.That(error.Message, Is.EqualTo(
            $"No test step handlers were found to perform any steps. " +
            $"The test session has been marked as declined."));
    }

    [Test]
    public void MissingUrlInTestSession()
    {
        // Act
        var error = Errors.MissingUrlInTestSession;

        // Assert
        Assert.That(error.Code, Is.EqualTo("1201"));
        Assert.That(error.Message, Is.EqualTo($"Missing URL in test session."));
    }

    [Test]
    public void MissingStartTimeInTestSession()
    {
        // Act
        var error = Errors.MissingStartTimeInTestSession;

        // Assert
        Assert.That(error.Code, Is.EqualTo("1202"));
        Assert.That(error.Message, Is.EqualTo($"Missing start time in test session."));
    }

    [Test]
    public void IncorrectStartDate()
    {
        // Act
        var error = Errors.IncorrectStartDate;

        // Assert
        Assert.That(error.Code, Is.EqualTo("1203"));
        Assert.That(error.Message, Is.EqualTo($"StartDate cannot be in the past."));
    }
}
