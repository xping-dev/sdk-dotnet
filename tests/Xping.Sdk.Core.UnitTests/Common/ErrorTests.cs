/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Common;

namespace Xping.Sdk.Common.UnitTests;

public sealed class ErrorTests
{
    [Test]
    public void ThrowsArgumentExceptionWhenCodeIsNull()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => new Error(code: null!, message: "message"));
    }

    [Test]
    public void ThrowsArgumentExceptionWhenMessageIsNull()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => new Error(code: "code", message: null!));
    }

    [Test]
    public void ThrowsArgumentExceptionWhenCodeIsNullAndMessageIsNull()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => new Error(code: null!, message: null!));
    }

    [Test]
    public void DoesNotThrowWhenCodeIsNotNullAndMessageIsNotNull()
    {
        // Assert
        Assert.DoesNotThrow(() => new Error(code: "code", message: "message"));
    }

    [Test]
    public void DoesNotThrowArgumentExceptionWhenCodeIsEmpty()
    {
        // Assert
        Assert.DoesNotThrow(() => new Error(code: string.Empty, message: "message"));
    }

    [Test]
    public void DoesNotThrowArgumentExceptionWhenMessageIsEmpty()
    {
        // Assert
        Assert.DoesNotThrow(() => new Error(code: "code", message: string.Empty));
    }

    [Test]
    public void DoesNotThrowArgumentExceptionWhenCodeIsEmptyAndMessageIsEmpty()
    {
        // Assert
        Assert.DoesNotThrow(() => new Error(code: string.Empty, message: string.Empty));
    }

    [Test]
    public void ToStringReturnsSpecificString()
    {
        // Arrange
        var error = new Error(code: "101", message: "Error message");

        // Assert
        Assert.That(error.ToString(), Is.EqualTo($"Error 101: Error message"));
    }

    [Test]
    public void ErrorNoneShouldNotHaveCodeOrMessage()
    {
        // Arrange
        var error = Error.None;

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(error.Code, Is.Empty);
            Assert.That(error.Message, Is.Empty);
        });
    }

    [Test]
    public void ErrorNoneShouldEqualToAnotherErrorNone()
    {
        // Arrange
        var error1 = Error.None;
        var error2 = Error.None;

        // Act
        bool result = error1.Equals(error2);

        // Arrange
        Assert.That(result, Is.True);
    }

    [Test]
    public void EqaulShouldReturnTrueWhenTwoInstancesAreEqaul()
    {
        // Arrange
        var error1 = new Error("code1", "meessage1");
        var error2 = new Error("code1", "meessage1");

        // Act
        bool result = error1.Equals(error2);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void EqaulShouldReturnTrueWhenTwoInstancesHaveDifferentMessageWithSameCode()
    {
        // Arrange
        var error1 = new Error("code1", "meessage");
        var error2 = new Error("code1", "different-meessage");

        // Act
        bool result = error1.Equals(error2);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void EqaulShouldReturnTrueWhenTwoInstancesAreReferenceEqual()
    {
        // Arrange
        var error1 = new Error("code1", "meessage");
        var error2 = error1;

        // Act
        bool result = error1.Equals(error2);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void EqaulShouldReturnFalseWhenAnotherInstanceIsNull()
    {
        // Arrange
        var error1 = new Error("code1", "meessage1");
        object? error2 = null;

        // Act
#pragma warning disable CA1508 // Avoid dead conditional code
        bool result = error1.Equals(error2);
#pragma warning restore CA1508 // Avoid dead conditional code

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void GetHashCodeShouldNotReturnZero()
    {
        // Arrange
        var error = new Error("code", "message");

        // Assert
        Assert.That(error.GetHashCode(), Is.Not.Zero);
    }

    [Test]
    public void EqaulOperatorShouldReturnTrueWhenTwoInstancesAreEqaul()
    {
        // Arrange
        var error1 = new Error("code1", "meessage1");
        var error2 = new Error("code1", "meessage1");

        // Act
        bool result = error1 == error2;

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void EqaulOperatorShouldReturnTrueWhenBothValuesAreNull()
    {
        // Arrange
        Error? error1 = null;
        Error? error2 = null;

        // Act
#pragma warning disable CA1508 // Avoid dead conditional code
        bool result = error1 == error2;
#pragma warning restore CA1508 // Avoid dead conditional code

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void InequalityOperatorShouldReturnFalseWhenTwoInstancesAreEqaul()
    {
        // Arrange
        var error1 = new Error("code1", "meessage1");
        var error2 = new Error("code1", "meessage1");

        // Act
        bool result = error1 != error2;

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void InequalityOperatorShouldReturnTrueWhenTwoInstancesAreNotEqual()
    {
        // Arrange
        var error1 = new Error("code1", "meessage1");
        var error2 = new Error("code2", "meessage2");

        // Act
        bool result = error1 != error2;

        // Assert
        Assert.That(result, Is.True);
    }
}
