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

public sealed class UrlComparerTests : ComparerBaseTests<UrlComparer>
{
    [Test]
    public void CompareShouldReturnDiffResultWhenUrlIsEqual()
    {
        // Arrange
        Uri url = new("http://test.com");

        using TestSession session1 = CreateTestSessionMock(url: url);
        using TestSession session2 = CreateTestSessionMock(url: url);

        // Act
        UrlComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result, Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenUrlIsNotEqual()
    {
        // Arrange
        Uri url1 = new("http://test1.com");
        Uri url2 = new("http://test2.com");

        using TestSession session1 = CreateTestSessionMock(url: url1);
        using TestSession session2 = CreateTestSessionMock(url: url2);

        // Act
        UrlComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Changed));
        Assert.That(result.Differences.First().Value1, Is.EqualTo(session1.Url));
        Assert.That(result.Differences.First().Value2, Is.EqualTo(session2.Url));
    }
}
