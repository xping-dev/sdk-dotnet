/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Session;
using Xping.Sdk.Core.Session.Comparison;
using Xping.Sdk.Core.Session.Comparison.Comparers;

namespace Xping.Sdk.Core.UnitTests.Session.Comparison;

public sealed class DeclineReasonComparerTests : ComparerBaseTests<DeclineReasonComparer>
{
    [Test]
    public void CompareShouldReturnDiffResultWhenDeclinedReasonIsEqual()
    {
        // Arrange
        const string declineReason = "DeclinedText";

        using TestSession session1 = CreateTestSessionMock(declineReason: declineReason);
        using TestSession session2 = CreateTestSessionMock(declineReason: declineReason);

        // Act
        DeclineReasonComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result, Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenDeclinedReasonIsNotEqual()
    {
        // Arrange
        const string declineReason1 = "DeclinedText1";
        const string declineReason2 = "DeclinedText2";

        using TestSession session1 = CreateTestSessionMock(declineReason: declineReason1);
        using TestSession session2 = CreateTestSessionMock(declineReason: declineReason2);

        // Act
        DeclineReasonComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Changed));
        Assert.That(result.Differences.First().Value1, Is.EqualTo(declineReason1));
        Assert.That(result.Differences.First().Value2, Is.EqualTo(declineReason2));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenDeclinedReasonIsAbsentInFirstButPresentInSecond()
    {
        // Arrange
        const string declineReason = "DeclinedText";

        using TestSession session1 = CreateTestSessionMock(declineReason: null);
        using TestSession session2 = CreateTestSessionMock(declineReason: declineReason);

        // Act
        DeclineReasonComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Added));
        Assert.That(result.Differences.First().Value1, Is.Null);
        Assert.That(result.Differences.First().Value2, Is.EqualTo(declineReason));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenDeclinedReasonIsAbsentInSecondButPresentInFirst()
    {
        // Arrange
        const string declineReason = "DeclinedText";

        using TestSession session1 = CreateTestSessionMock(declineReason: declineReason);
        using TestSession session2 = CreateTestSessionMock(declineReason: null);

        // Act
        DeclineReasonComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Removed));
        Assert.That(result.Differences.First().Value1, Is.EqualTo(declineReason));
        Assert.That(result.Differences.First().Value2, Is.Null);
    }
}
