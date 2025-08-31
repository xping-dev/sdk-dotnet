/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Moq;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Core.Session.Comparison;

namespace Xping.Sdk.Core.UnitTests.Session.Comparison;

public sealed class DiffEngineTests
{
    [Test]
    public void HasZeroComparersWhenInstantiatedWithNoParameters()
    {
        // Arrange
        var diffEngine = new DiffEngine();

        // Assert
        Assert.That(diffEngine.Comparers, Has.Count.EqualTo(0));
    }

    [Test]
    public void HasTwoComparersWhenInstantiatedWithTwoParameters()
    {
        // Arrange
        var diffEngine = new DiffEngine(Mock.Of<ITestSessionComparer>(), Mock.Of<ITestSessionComparer>());

        // Assert
        Assert.That(diffEngine.Comparers, Has.Count.EqualTo(2));
    }

    [Test]
    public void DoesNotThrowWhenInstantiatedWithNullComparers()
    {
        Assert.DoesNotThrow(() => new DiffEngine(comparers: null!));
    }

    [Test]
    public void ExecuteDiffReturnsDiffResultEmptyWhenBothTestSessionsAreNull()
    {
        // Arrange
        var diffEngine = new DiffEngine();
        TestSession session1 = null!;
        TestSession session2 = null!;

        // Assert
        Assert.That(diffEngine.ExecuteDiff(session1, session2), Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void ExecuteDiffReturnsDiffResultEmptyWhenOneTestSessionIsNull()
    {
        // Arrange
        var diffEngine = new DiffEngine();
        using TestSession session1 = new()
        {
            StartDate = DateTime.Now,
            State = TestSessionState.NotStarted,
            Steps = [],
            Url = new Uri("http://localhost"),
            TestSettings = new Xping.Sdk.Core.Components.TestSettings()
        };
        TestSession session2 = null!;

        // Assert
        Assert.That(diffEngine.ExecuteDiff(session1, session2), Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void ExecuteDiffReturnsDiffResultEmptyWhenBothTestSessionAreReferenceEqual()
    {
        // Arrange
        var diffEngine = new DiffEngine();
        using TestSession session1 = new()
        {
            StartDate = DateTime.Now,
            State = TestSessionState.NotStarted,
            Steps = [],
            Url = new Uri("http://localhost"),
            TestSettings = new Xping.Sdk.Core.Components.TestSettings()
        };
        TestSession session2 = session1;

        // Assert
        Assert.That(diffEngine.ExecuteDiff(session1, session2), Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void ExceuteDiffInvokesAllRegisteredComparers()
    {
        // Arrange
        ITestSessionComparer[] comparers = [Mock.Of<ITestSessionComparer>(), Mock.Of<ITestSessionComparer>()];
        var diffEngine = new DiffEngine(comparers);

        using TestSession session1 = new()
        {
            StartDate = DateTime.Now,
            State = TestSessionState.NotStarted,
            Steps = [],
            Url = new Uri("http://localhost"),
            TestSettings = new Xping.Sdk.Core.Components.TestSettings()
        };

        using TestSession session2 = new()
        {
            StartDate = DateTime.Now,
            State = TestSessionState.NotStarted,
            Steps = [],
            Url = new Uri("http://localhost"),
            TestSettings = new Xping.Sdk.Core.Components.TestSettings()
        };

        // Act
        DiffResult result = diffEngine.ExecuteDiff(session1, session2);

        // Assert
        for (int i = 0; i < comparers.Length; i++)
        {
            Mock.Get(comparers[i]).Verify(c => c.Compare(
                It.Is<TestSession>(ts => ts == session1),
                It.Is<TestSession>(ts => ts == session2)), Times.Once);
        }
    }
}
