/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Exceptions;

using Xping.Sdk.Core.Exceptions;

public sealed class XpingNetworkExceptionTests
{
    [Fact]
    public void DefaultConstructor_ShouldCreateException()
    {
        // Act
        var exception = new XpingNetworkException();

        // Assert
        Assert.IsType<XpingNetworkException>(exception);
    }

    [Fact]
    public void MessageConstructor_ShouldSetMessage()
    {
        // Arrange
        const string message = "Xping network error in strict mode: HTTP request failed.";

        // Act
        var exception = new XpingNetworkException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void InnerExceptionConstructor_ShouldSetMessageAndInnerException()
    {
        // Arrange
        const string message = "Xping network error in strict mode: HTTP request failed.";
        var inner = new InvalidOperationException("inner");

        // Act
        var exception = new XpingNetworkException(message, inner);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void ShouldBeSubclassOfException()
    {
        // Act
        var exception = new XpingNetworkException("test");

        // Assert
        Assert.IsAssignableFrom<Exception>(exception);
    }
}
