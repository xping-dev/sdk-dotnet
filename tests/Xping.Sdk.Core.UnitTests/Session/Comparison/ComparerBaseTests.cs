/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Core.Session.Comparison;

namespace Xping.Sdk.Core.UnitTests.Session.Comparison;

public abstract class ComparerBaseTests<TComparer> where TComparer : ITestSessionComparer, new()
{
    protected static TestSession CreateTestSessionMock(
    Uri? url = null,
    DateTime? startDate = null,
    TestSessionState state = TestSessionState.NotStarted,
    string? declineReason = null,
    ICollection<TestStep>? steps = null) => new()
    {
        Url = url ?? new Uri("https://test"),
        StartDate = startDate ?? DateTime.UtcNow,
        Steps = steps?.ToList().AsReadOnly() ?? new List<TestStep>().AsReadOnly(),
        State = declineReason != null ? TestSessionState.Declined : state,
        DeclineReason = declineReason
    };

    protected static TestStep CreateTestStepMock(
        string name = "TestStepMock",
        DateTime? startDate = null,
        TimeSpan? duration = null,
        TestStepType type = TestStepType.ActionStep,
        TestStepResult result = TestStepResult.Succeeded,
        PropertyBag<IPropertyBagValue>? propertyBag = null,
        string? errorMessage = null) => new()
        {
            Name = name,
            TestComponentIteration = 1,
            StartDate = startDate ?? DateTime.UtcNow,
            Duration = duration ?? TimeSpan.Zero,
            Type = type,
            Result = result,
            ErrorMessage = errorMessage,
            PropertyBag = propertyBag
        };

    [Test]
    public void CompareReturnsDiffResultEmptyWhenTwoTestSessionsAreReferenceEqual()
    {
        // Arrange
        using TestSession session1 = CreateTestSessionMock();
        TestSession session2 = session1;

        // Act
        var comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result, Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void CompareReturnsDiffResultEmptyWhenTwoTestSessionsAreNull()
    {
        // Arrange
        TestSession session1 = null!;
        TestSession session2 = null!;

        // Act
        var comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result, Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void CompareReturnsDiffResultEmptyWhenFirstTestSessionIsNull()
    {
        // Arrange
        TestSession session1 = null!;
        using TestSession session2 = CreateTestSessionMock();

        // Act
        var comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result, Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void CompareReturnsDiffResultEmptyWhenSecondTestSessionIsNull()
    {
        // Arrange
        using TestSession session1 = CreateTestSessionMock();
        TestSession session2 = null!;

        // Act
        var comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result, Is.EqualTo(DiffResult.Empty));
    }

    protected TComparer CreateComparer() => new();
}
