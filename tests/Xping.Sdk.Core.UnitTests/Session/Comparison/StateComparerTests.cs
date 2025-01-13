/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Session.Comparison;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Core.Session.Comparison.Comparers;

namespace Xping.Sdk.Core.UnitTests.Session.Comparison;

public sealed class StateComparerTests : ComparerBaseTests<StateComparer>
{
    [Test]
    public void CompareShouldReturnDiffResultWhenStateIsEqual()
    {
        // Arrange
        const TestSessionState state = TestSessionState.Completed;

        using TestSession session1 = CreateTestSessionMock(state: state);
        using TestSession session2 = CreateTestSessionMock(state: state);

        // Act
        StateComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result, Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenStateIsNotEqual()
    {
        // Arrange
        const TestSessionState state1 = TestSessionState.Completed;
        const TestSessionState state2 = TestSessionState.NotStarted;

        using TestSession session1 = CreateTestSessionMock(state: state1);
        using TestSession session2 = CreateTestSessionMock(state: state2);

        // Act
        StateComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Changed));
        Assert.That(result.Differences.First().Value1, Is.EqualTo(session1.State));
        Assert.That(result.Differences.First().Value2, Is.EqualTo(session2.State));
    }
}
