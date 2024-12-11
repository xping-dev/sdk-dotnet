using Moq;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using TestContext = Xping.Sdk.Core.Components.TestContext;

namespace Xping.Sdk.UnitTests.Session;

public sealed class TestSessionBuilderTests
{
    [Test]
    public void PropertyBagIsNotNullAfterBuilderCreattion()
    {
        // Act
        var builder = new TestSessionBuilder();

        // Assert
        Assert.That(builder, Is.Not.Null);
    }

    [Test]
    public void HasFailedReturnsFalseAfterBuilderCreation()
    {
        // Act
        var builder = new TestSessionBuilder();

        // Assert
        Assert.That(builder.HasFailed, Is.False);
    }

    [Test]
    public void HasFailedReturnsTrueWhenFailingStepHasBeenAdded()
    {
        // Arrange
        const bool expectedResult = true;

        var builder = new TestSessionBuilder();
        using var instrumentation = new InstrumentationTimer();
        var mockedComponent = new Mock<ITestComponent>();
        mockedComponent.SetupGet(c => c.Name).Returns("ComponentName");
        mockedComponent.SetupGet(c => c.Type).Returns(TestStepType.ActionStep);

        var context = new TestContext(builder, instrumentation, progress: null);
        context.UpdateExecutionContext(mockedComponent.Object);

        builder.Initiate(
            url: new Uri("http://localhost/"),
            startDate: DateTime.UtcNow,
            context: context);

        // Act
        builder.Build(new Error("code", "message"));

        // Assert
        Assert.That(builder.HasFailed, Is.EqualTo(expectedResult));
    }

    [Test]
    public void HasCorrectTestComponentIterationWhenMultipleTestStepsHaveBeenAddedForTheSameComponent()
    {
        // Arrange
        const int expectedCount = 4;

        var builder = new TestSessionBuilder();
        using var instrumentation = new InstrumentationTimer();
        var mockedComponent = new Mock<ITestComponent>();
        mockedComponent.SetupGet(c => c.Name).Returns("ComponentName");
        mockedComponent.SetupGet(c => c.Type).Returns(TestStepType.ActionStep);

        var context = new TestContext(builder, instrumentation, progress: null);
        context.UpdateExecutionContext(mockedComponent.Object);

        builder.Initiate(
            url: new Uri("http://localhost/"),
            startDate: DateTime.UtcNow,
            context: context);

        // Act
        builder.Build();
        builder.Build();
        builder.Build();
        builder.Build();

        TestSession session = builder.GetTestSession();
        int counter = 0;
        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(session.Steps.All(s => s.TestComponentIteration == ++counter), Is.True);
            Assert.That(counter, Is.EqualTo(expectedCount));
        });
    }

    [Test]
    public void TestComponentIterationCountShoudStartFrom1()
    {
        // Arrange
        var builder = new TestSessionBuilder();
        using var instrumentation = new InstrumentationTimer();
        var mockedComponent = new Mock<ITestComponent>();
        mockedComponent.SetupGet(c => c.Name).Returns("ComponentName");
        mockedComponent.SetupGet(c => c.Type).Returns(TestStepType.ActionStep);

        var context = new TestContext(builder, instrumentation, progress: null);
        context.UpdateExecutionContext(mockedComponent.Object);

        builder.Initiate(
            url: new Uri("http://localhost/"),
            startDate: DateTime.UtcNow,
            context: context);

        // Act
        TestStep step = builder.Build();

        // Assert
        Assert.That(step.TestComponentIteration, Is.EqualTo(1));
    }

    [Test]
    public void GetTestSessionReturnsDeclinedSessionWhenBuilderNotInitiated()
    {
        // Act
        var builder = new TestSessionBuilder();

        // Assert
        Assert.That(builder.GetTestSession().State, Is.EqualTo(TestSessionState.Declined));
    }

    [Test]
    public void DeclinedReasonHasSpecificTextWhenUrlIsNullDuringBuilderInitialization()
    {
        // Arrange
        string expectedDeclineReason = Errors.MissingUrlInTestSession;
        using var instrumentation = new InstrumentationTimer();
        var builder = new TestSessionBuilder();

        // Act
        builder.Initiate(
            url: null!, 
            startDate: DateTime.UtcNow, 
            context: new TestContext(builder, instrumentation, progress: null));

        // Assert
        Assert.That(builder.GetTestSession().DeclineReason, Does.StartWith(expectedDeclineReason));
    }

    [Test]
    public void DeclinedReasonHasSpecificTextWhenStartDateIsInThePast()
    {
        // Arrange
        string expectedDeclineReason = Errors.IncorrectStartDate;
        using var instrumentation = new InstrumentationTimer();
        var builder = new TestSessionBuilder();

        // Act
        builder.Initiate(
            url: new Uri("http://test.com"), 
            startDate: DateTime.UtcNow - TimeSpan.FromDays(2),
            context: new TestContext(builder, instrumentation, progress: null));

        // Assert
        Assert.That(builder.GetTestSession().DeclineReason, Does.StartWith(expectedDeclineReason));
    }
}
