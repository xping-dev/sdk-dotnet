/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Exceptions;

using Xping.Sdk.Core.Exceptions;

public sealed class XpingConfigurationExceptionTests
{
    [Fact]
    public void DefaultConstructor_ShouldCreateException()
    {
        // Act
        var exception = new XpingConfigurationException();

        // Assert
        Assert.IsType<XpingConfigurationException>(exception);
    }

    [Fact]
    public void MessageConstructor_ShouldSetMessage()
    {
        // Arrange
        const string message = "Xping configuration invalid: ApiKey is required.";

        // Act
        var exception = new XpingConfigurationException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void InnerExceptionConstructor_ShouldSetMessageAndInnerException()
    {
        // Arrange
        const string message = "Xping configuration invalid: ApiKey is required.";
        var inner = new InvalidOperationException("inner");

        // Act
        var exception = new XpingConfigurationException(message, inner);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void ShouldBeSubclassOfException()
    {
        // Act
        var exception = new XpingConfigurationException("test");

        // Assert
        Assert.IsAssignableFrom<Exception>(exception);
    }
}
