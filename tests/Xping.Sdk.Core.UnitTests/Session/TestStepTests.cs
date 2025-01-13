/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Session;

namespace Xping.Sdk.UnitTests.Session;

public sealed class TestStepTests
{
    [Test]
    public void ThrowsArgumentExceptionWhenStartDateIsInThePast()
    {
        // Assert
        Assert.Throws<ArgumentException>(() => new TestStep()
        {
            Name = "TestStepName",
            TestComponentIteration = 1,
            StartDate = DateTime.UtcNow - TimeSpan.FromDays(2),
            Duration = TimeSpan.Zero,
            Type = TestStepType.ActionStep,
            Result = TestStepResult.Succeeded,
            PropertyBag = null,
            ErrorMessage = null,
        });
    }

    [Test]
    public void DoesNotThrowWhenStartDateIsNotInThePast()
    {
        // Assert
        Assert.DoesNotThrow(() => new TestStep()
        {
            Name = "TestStepName",
            TestComponentIteration = 1,
            StartDate = DateTime.Today,
            Duration = TimeSpan.Zero,
            Type = TestStepType.ActionStep,
            Result = TestStepResult.Succeeded,
            PropertyBag = null,
            ErrorMessage = null,
        });
    }

    [Test]
    public void ThrowsArgumentExceptionWhenNameIsNullOrEmpty()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => new TestStep()
        {
            Name = null!,
            TestComponentIteration = 1,
            StartDate = DateTime.UtcNow,
            Duration = TimeSpan.Zero,
            Type = TestStepType.ActionStep,
            Result = TestStepResult.Succeeded,
            PropertyBag = null,
            ErrorMessage = "err msg",
        });
    }

    [Test]
    public void DoesNotThrowArgumentExceptionWhenPropertyBagIsNull()
    {
        // Assert
        Assert.DoesNotThrow(() => new TestStep()
        {
            Name = "TestStepName",
            TestComponentIteration = 1,
            StartDate = DateTime.UtcNow,
            Duration = TimeSpan.Zero,
            Type = TestStepType.ActionStep,
            Result = TestStepResult.Succeeded,
            PropertyBag = null,
            ErrorMessage = null,
        });
    }

    [Ignore(reason: "Investigate possibility how to implement this requirement")]
    [Test]
    public void ThrowsArgumentExceptionWhenResulIsFailedAndErrorMessageIsNullOrEmpty()
    {
        // Assert
        Assert.Throws<ArgumentException>(() => new TestStep()
        {
            Name = "TestStepName",
            TestComponentIteration = 1,
            StartDate = DateTime.UtcNow,
            Duration = TimeSpan.Zero,
            Type = TestStepType.ActionStep,
            Result = TestStepResult.Failed,
            PropertyBag = null,
            ErrorMessage = null,
        });
    }

    [Ignore(reason: "Investigate possibility how to implement this requirement")]
    [Test]
    public void ErrorMessageIsNullWhenResultIsSucceeded()
    {
        // Arrange
        var testStep = new TestStep()
        {
            Name = "TestStepName",
            TestComponentIteration = 1,
            StartDate = DateTime.UtcNow,
            Duration = TimeSpan.Zero,
            Type = TestStepType.ActionStep,
            Result = TestStepResult.Succeeded,
            PropertyBag = null,
            ErrorMessage = "Error message",
        };

        // Assert
        Assert.That(testStep.ErrorMessage, Is.Null);
    }
}
