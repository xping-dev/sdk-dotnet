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

public sealed class TestSessionTests
{
    private static TestSession CreateTestSessionUnderTest(
        Uri? url = null,
        DateTime? startDate = null,
        ICollection<TestStep>? steps = null) => new()
        {
            Url = url ?? new Uri("https://test"),
            StartDate = startDate ?? DateTime.UtcNow,
            Steps = steps?.ToList().AsReadOnly() ?? new List<TestStep>().AsReadOnly(),
            State = TestSessionState.NotStarted,
            TestSettings = new TestSettings()
        };

    private static TestStep CreateTestStepMock(
        string name = "TestStepMock",
        DateTime? startDate = null,
        TimeSpan? duration = null,
        TestStepType type = TestStepType.ActionStep,
        TestStepResult result = TestStepResult.Succeeded,
        string? errorMessage = null) => new()
        {
            Name = name,
            Description = "Test step description",
            DisplayName = "Test Step Display Name",
            TestComponentIteration = 1,
            StartDate = startDate ?? DateTime.UtcNow,
            Duration = duration ?? TimeSpan.Zero,
            Type = type,
            Result = result,
            ErrorMessage = errorMessage,
            PropertyBag = null
        };

    [Test]
    public void ConstructorAssignsGUIDWhenAnObjectIsInstantiated()
    {
        // Act
        using var session = new TestSession
        {
            Url = new Uri("http://localhost"),
            StartDate = DateTime.UtcNow,
            Steps = [],
            State = TestSessionState.NotStarted,
            TestSettings = new TestSettings()
        };

        // Assert
        Assert.That(session.Id, Is.Not.EqualTo(Guid.Empty));
    }


    [Test]
    public void ThrowsArgumentNullExceptionWhenInstantiatedWithNullUri()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => new TestSession
        {
            Url = null!,
            StartDate = DateTime.UtcNow,
            Steps = [],
            State = TestSessionState.NotStarted,
            TestSettings = new TestSettings()
        });
    }

    [Test]
    public void NotStartedWhenNewlyInstantiated()
    {
        // Arrange
        using var testSession = CreateTestSessionUnderTest();

        // Assert
        Assert.That(testSession.State, Is.EqualTo(TestSessionState.NotStarted));
    }

    [Test]
    public void HasZeroStepsWhenInstantiatedWithZeroSteps()
    {
        // Arrange
        using var testSession = CreateTestSessionUnderTest(steps: []);

        // Assert
        Assert.That(testSession.Steps, Is.Empty);
    }

    [Test]
    public void HasZeroFailuresWhenNoFailuresHasBeenAdded()
    {
        // Arrange
        using var testSession = CreateTestSessionUnderTest(steps: []);

        // Assert
        Assert.That(testSession.Failures, Is.Empty);
    }

    [Test]
    public void IsValidWhenNoFailuresHasBeenGiven()
    {
        // Arrange
        using var testSession = CreateTestSessionUnderTest(steps: [CreateTestStepMock()]);

        // Assert
        Assert.That(testSession.IsValid, Is.True);
    }

    [Test]
    public void HasFailedTestStepsWhenFailedTestStepHasBeenAdded()
    {
        // Arrange
        const int expectedItemCount = 1;

        // Act
        using var testSession = CreateTestSessionUnderTest(
            steps: [CreateTestStepMock(result: TestStepResult.Failed, errorMessage: "error")]);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(testSession.Steps, Has.Count.EqualTo(expectedItemCount));
            Assert.That(testSession.Failures, Has.Count.EqualTo(expectedItemCount));
        });
    }

    [Test]
    public void HasZeroFailedTestStepsWhenSuccessTestStepHasBeenAdded()
    {
        // Arrange
        const int expectedItemCount = 0;

        // Act
        using var testSession = CreateTestSessionUnderTest(steps: [CreateTestStepMock()]);

        // Assert
        Assert.That(testSession.Failures, Has.Count.EqualTo(expectedItemCount));
    }

    [Test]
    public void DurationShowsTotalElapsedTimeFromSessionStartToLastStepEnd()
    {
        // Arrange
        var sessionStartDate = DateTime.UtcNow;
        var steps = new List<TestStep>();

        // Create steps with realistic timing - each starting after the previous one ends
        var currentTime = sessionStartDate.AddMilliseconds(100); // First step starts 100ms after session
        for (int i = 0; i < 3; i++)
        {
            steps.Add(CreateTestStepMock(
                name: $"Step{i + 1}",
                startDate: currentTime,
                duration: TimeSpan.FromSeconds(2))); // Each step takes 2 seconds
            
            currentTime = currentTime.AddSeconds(2.5); // Next step starts 2.5 seconds later (2s duration + 0.5s gap)
        }

        // Expected duration: from session start to end of last step
        // Last step ends at: sessionStart + 0.1s + (2.5s * 2) + 2s = sessionStart + 7.1s
        var expectedDuration = TimeSpan.FromSeconds(7.1);

        // Act
        using var testSession = CreateTestSessionUnderTest(
            startDate: sessionStartDate,
            steps: steps);

        // Assert
        Assert.That(testSession.Duration, Is.EqualTo(expectedDuration));
    }

    [Test]
    public void IsEqualToOtherTestSessionWithTheSameId()
    {
        // Arrange
        using TestSession session1 = CreateTestSessionUnderTest();
        var serializer = new TestSessionSerializer();
        
        using var stream = new MemoryStream();
        serializer.Serialize(session1, stream, SerializationFormat.XML);
        stream.Position = 0;

        TestSession? session2 = serializer.Deserialize(stream, SerializationFormat.XML);

        // Assert
        Assert.That(session1.Equals(session2), Is.True);
    }
}
