/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace XPing365.Sdk.Common.UnitTests;

public sealed class ArgumentValidationTests
{
    [Test]
    public void RequireNotNullThrowsWhenParameterNameIsNullAndValueIsNull()
    {
        // Arrange
        object value = null!;

        // Assert
        Assert.Throws<ArgumentNullException>(() => _ = value.RequireNotNull(parameterName: null!));
    }

    [Test]
    public void RequireNotNullThrowsWhenParameterNameIsNullAndValueIsNotNull()
    {
        // Arrange
        object value = new();

        // Assert
        Assert.Throws<ArgumentNullException>(() => _ = value.RequireNotNull(parameterName: null!));
    }


    [Test]
    public void RequireNotNullDoesNotThrowWhenArgumentIsNotNull()
    {
        // Arrange
        object value = new();

        // Assert
        Assert.DoesNotThrow(() => _ = value.RequireNotNull(nameof(value)));
    }

    [Test]
    public void RequireNotNullThrowsWhenArgumentIsNull()
    {
        // Arrange
        object value = null!;

        // Assert
        Assert.Throws<ArgumentNullException>(() => _ = value.RequireNotNull(nameof(value)));
    }

    [Test]
    public void RequireNotNullThrowsSpecificTextWhenValueIsNull()
    {
        // Arrange
        const string expectedMessage = "Argument value is null.";
        object value = null!;

        // Assert
        var ex = Assert.Throws<ArgumentNullException>(
            code: () => _ = value.RequireNotNull(nameof(value)));
        Assert.That(ex.Message.StartsWith(expectedMessage, StringComparison.InvariantCulture));
    }

    [Test]
    public void RequireConditionThrowsArgumentNullExceptionWhenObjectIsNull()
    {
        // Arrange
        object value = null!;

        // Assert
        Assert.Throws<ArgumentNullException>(() => value.RequireCondition(obj => true, nameof(value), "message"));
    }

    [Test]
    public void RequireConditionThrowsArgumentNullExceptionWhenConditionIsNull()
    {
        // Arrange
        object value = new();

        // Assert
        Assert.Throws<ArgumentNullException>(() => value.RequireCondition(condition: null!, nameof(value), "message"));
    }

    [Test]
    public void RequireConditionThrowsArgumentNullExceptionWhenParameterIsNull()
    {
        // Arrange
        object value = new();

        // Assert
        Assert.Throws<ArgumentNullException>(() => value.RequireCondition(
            obj => true,
            parameterName: null!,
            "message"));
    }

    [Test]
    public void RequireConditionThrowsArgumentNullExceptionWhenMessageIsNull()
    {
        // Arrange
        object value = new();

        // Assert
        Assert.Throws<ArgumentNullException>(() => value.RequireCondition(obj => true, nameof(value), message: null!));
    }

    [Test]
    public void RequireConditionDoesNotThrowWhenConditionResolvesToTrue()
    {
        // Arrange
        object value = new();

        // Assert
        Assert.DoesNotThrow(() => value.RequireCondition(obj => true, nameof(value), "message"));
    }

    [Test]
    public void RequireConditionReturnsTheSameObjectThatWasGiven()
    {
        // Arrange
        object value = new();

        // Assert
        Assert.That(value.RequireCondition(obj => true, nameof(value), "message"), Is.EqualTo(value));
    }

    [Test]
    public void RequireConditionReturnsGivenMessageInExceptionWhenConditionResolvesToFalse()
    {
        // Arrange
        const string expectedMessage = "Condition not met.";
        object value = new();

        // Assert
        var ex = Assert.Throws<ArgumentException>(() => value.RequireCondition(
            obj => false,
            nameof(value),
            message: expectedMessage));

        Assert.That(ex.Message.StartsWith(expectedMessage, StringComparison.InvariantCulture));
    }

    [Test]
    public void RequireNotNullOrWhiteSpaceThrowsArgumentNullExceptionWhenParameterIsNull()
    {
        // Arrange
        string value = "text";

        // Assert
        Assert.Throws<ArgumentNullException>(() => value.RequireNotNullOrWhiteSpace(parameterName: null!));
    }

    [Test]
    public void RequireNotNullOrWhiteSpaceThrowsArgumentExceptionWhenValueIsNull()
    {
        // Arrange
        string value = null!;

        // Assert
        Assert.Throws<ArgumentException>(() => value.RequireNotNullOrWhiteSpace(nameof(value)));
    }

    [Test]
    public void RequireNotNullOrWhiteSpaceThrowsArgumentExceptionWhenValueIsWhitespace()
    {
        // Arrange
        string value = "    ";

        // Assert
        Assert.Throws<ArgumentException>(() => value.RequireNotNullOrWhiteSpace(nameof(value)));
    }

    [Test]
    public void RequireNotNullOrWhiteSpaceDoesNotThrowWhenValueIsNotNullOrWhitespace()
    {
        // Arrange
        string value = "test";

        // Assert
        Assert.DoesNotThrow(() => value.RequireNotNullOrWhiteSpace(nameof(value)));
    }

    [Test]
    public void RequireNotNullOrWhiteSpaceReturnsTheSameGivenString()
    {
        // Arrange
        const string expectedValue = "test";
        string value = expectedValue;

        // Assert
        Assert.That(value.RequireNotNullOrWhiteSpace(nameof(value)), Is.EqualTo(expectedValue));
    }

    [Test]
    public void RequireNotNullOrEmptyThrowsArgumentNullExceptionWhenParameterIsNull()
    {
        // Arrange
        string value = "text";

        // Assert
        Assert.Throws<ArgumentNullException>(() => value.RequireNotNullOrEmpty(parameterName: null!));
    }

    [Test]
    public void RequireNotNullOrEmptyThrowsArgumentExceptionWhenValueIsNull()
    {
        // Arrange
        string value = null!;

        // Assert
        Assert.Throws<ArgumentException>(() => value.RequireNotNullOrEmpty(nameof(value)));
    }

    [Test]
    public void RequireNotNullOrEmptyThrowsArgumentExceptionWhenValueIsEmpty()
    {
        // Arrange
        string value = string.Empty;

        // Assert
        Assert.Throws<ArgumentException>(() => value.RequireNotNullOrEmpty(nameof(value)));
    }

    [Test]
    public void RequireNotNullOrEmptyDoesNotThrowWhenValueIsNotNullOrWhitespace()
    {
        // Arrange
        string value = "test";

        // Assert
        Assert.DoesNotThrow(() => value.RequireNotNullOrEmpty(nameof(value)));
    }

    [Test]
    public void RequireNotNullOrEmptyReturnsTheSameGivenString()
    {
        // Arrange
        const string expectedValue = "test";
        string value = expectedValue;

        // Assert
        Assert.That(value.RequireNotNullOrEmpty(nameof(value)), Is.EqualTo(expectedValue));
    }
}
