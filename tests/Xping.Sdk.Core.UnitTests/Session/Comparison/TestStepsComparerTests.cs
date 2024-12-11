using Xping.Sdk.Core.Session.Comparison;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Core.Session.Comparison.Comparers;
using Xping.Sdk.Core.Common;
using System.Runtime.Intrinsics.X86;

namespace Xping.Sdk.Core.UnitTests.Session.Comparison;

public sealed class TestStepsComparerTests : ComparerBaseTests<TestStepsComparer>
{
    [Test]
    public void CompareShouldReturnDiffResultEmptyWhenStepsAreEqual()
    {
        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(duration: TimeSpan.FromMilliseconds(100)),
            CreateTestStepMock(duration: TimeSpan.FromMilliseconds(100)),
            CreateTestStepMock(duration: TimeSpan.FromMilliseconds(100)),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(duration: TimeSpan.FromMilliseconds(100)),
            CreateTestStepMock(duration: TimeSpan.FromMilliseconds(100)),
            CreateTestStepMock(duration: TimeSpan.FromMilliseconds(100)),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result, Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenOneStepIsPresentInSecondAndAbsentInFirst()
    {
        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(name: "Step1"),
            CreateTestStepMock(name: "Step2"),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: "Step1"),
            CreateTestStepMock(name: "Step2"),
            CreateTestStepMock(name: "Step3"),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Added));
        Assert.That(
            result.Differences.First().PropertyName,
            Is.EqualTo($"{nameof(TestStep)}(\"Step3\").{nameof(TestStep.TestComponentIteration)}[\"1\"]"));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenOneStepIsPresentInFirstAndAbsentInSecond()
    {
        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(name: "Step1"),
            CreateTestStepMock(name: "Step2"),
            CreateTestStepMock(name: "Step3"),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: "Step1"),
            CreateTestStepMock(name: "Step2"),            
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Removed));
        Assert.That(
            result.Differences.First().PropertyName,
            Is.EqualTo($"{nameof(TestStep)}(\"Step3\").{nameof(TestStep.TestComponentIteration)}[\"1\"]"));
    }

    [Test]
    public void CompareShouldReturnDiffResultEmptyRegardlessOfStartDate()
    {
        // Since TestStep prohibits past StartDates, we initialize it with today's date and increment for
        // subsequent steps.
        var today = DateTime.Today;

        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(startDate: today + TimeSpan.FromDays(1)),
            CreateTestStepMock(startDate: today + TimeSpan.FromDays(2)),
            CreateTestStepMock(startDate: today + TimeSpan.FromDays(3))
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(startDate: today + TimeSpan.FromDays(4)),
            CreateTestStepMock(startDate: today + TimeSpan.FromDays(5)),
            CreateTestStepMock(startDate: today + TimeSpan.FromDays(6))
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result, Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void CompareShouldReturnDiffResultEmptyWhenStartDateIsEqual()
    {
        // Since TestStep prohibits past StartDates, we initialize it with today's date and increment for
        // subsequent steps.
        var startDate = DateTime.Today + TimeSpan.FromDays(1);

        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(startDate: startDate)
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(startDate: startDate)
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result, Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenStepsHasDifferentDuration()
    {
        const string StepName = "Step";

        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(name: StepName, duration: TimeSpan.FromMilliseconds(100)),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: StepName, duration: TimeSpan.FromMilliseconds(120)),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Changed));
        Assert.That(result.Differences.First().Value1, Is.EqualTo(steps1.First().Duration));
        Assert.That(result.Differences.First().Value2, Is.EqualTo(steps2.First().Duration));
        Assert.That(
            result.Differences.First().PropertyName,
            Is.EqualTo($"{nameof(TestStep)}(\"{StepName}\").{nameof(TestStep.Duration)}"));
    }

    [Test]
    public void CompareShouldReturnDiffResultEmptyWhenStepsHasDurationEqual()
    {
        const string StepName = "Step";
        var duration = TimeSpan.FromMilliseconds(100);

        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(name: StepName, duration: duration),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: StepName, duration: duration),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result, Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenStepsHasDifferentType()
    {
        const string StepName = "StepName";

        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(name: StepName, type: TestStepType.ActionStep),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: StepName, type: TestStepType.ValidateStep),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Changed));
        Assert.That(result.Differences.First().Value1, Is.EqualTo(steps1.First().Type));
        Assert.That(result.Differences.First().Value2, Is.EqualTo(steps2.First().Type));
        Assert.That(
            result.Differences.First().PropertyName,
            Is.EqualTo($"{nameof(TestStep)}(\"{StepName}\").{nameof(TestStep.Type)}"));
    }

    [Test]
    public void CompareShouldReturnDiffResultEmptyWhenStepsHasTypeEqual()
    {
        const string StepName = "StepName";
        var type = TestStepType.ActionStep;

        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(name: StepName, type: type),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: StepName, type: type),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result, Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenStepsHasDifferentResultType()
    {
        const string StepName = "StepName";

        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(name: StepName, result: TestStepResult.Succeeded),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: StepName, result: TestStepResult.Failed),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Changed));
        Assert.That(result.Differences.First().Value1, Is.EqualTo(steps1.First().Result));
        Assert.That(result.Differences.First().Value2, Is.EqualTo(steps2.First().Result));
        Assert.That(
            result.Differences.First().PropertyName,
            Is.EqualTo($"{nameof(TestStep)}(\"{StepName}\").{nameof(TestStep.Result)}"));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenStepsHasResultTypeEqual()
    {
        const string StepName = "StepName";
        var stepResult = TestStepResult.Succeeded;

        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(name: StepName, result: stepResult),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: StepName, result: stepResult),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result, Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenStepsHasDifferentErrorMessage()
    {
        const string StepName = "StepName";

        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(name: StepName, errorMessage: "error1"),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: StepName, errorMessage: "error2"),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Changed));
        Assert.That(result.Differences.First().Value1, Is.EqualTo(steps1.First().ErrorMessage));
        Assert.That(result.Differences.First().Value2, Is.EqualTo(steps2.First().ErrorMessage));
        Assert.That(
            result.Differences.First().PropertyName,
            Is.EqualTo($"{nameof(TestStep)}(\"{StepName}\").{nameof(TestStep.ErrorMessage)}"));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenStepsHasErrorMessageEqual()
    {
        const string StepName = "StepName";
        const string ErrorMessage = "Error message";

        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(name: StepName, errorMessage: ErrorMessage),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: StepName, errorMessage: ErrorMessage),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result, Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenErrorMessageIsAbsentInFirstButPresentInSecond()
    {
        const string StepName = "StepName";

        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(name: StepName, errorMessage: null),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: StepName, errorMessage: "error2"),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Added));
        Assert.That(result.Differences.First().Value1, Is.Null);
        Assert.That(result.Differences.First().Value2, Is.EqualTo(steps2.First().ErrorMessage));
        Assert.That(
            result.Differences.First().PropertyName,
            Is.EqualTo($"{nameof(TestStep)}(\"{StepName}\").{nameof(TestStep.ErrorMessage)}"));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenErrorMessageIsAbsentInSecondButPresentInFirst()
    {
        const string StepName = "StepName";

        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(name: StepName, errorMessage: "erro1"),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: StepName, errorMessage: null),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Removed));
        Assert.That(result.Differences.First().Value1, Is.EqualTo(steps1.First().ErrorMessage));
        Assert.That(result.Differences.First().Value2, Is.Null);
        Assert.That(
            result.Differences.First().PropertyName,
            Is.EqualTo($"{nameof(TestStep)}(\"{StepName}\").{nameof(TestStep.ErrorMessage)}"));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenPropertyBagIsAbsentInSecondButPresentInFirst()
    {
        const string StepName = "StepName";

        // Arrange
        IList<TestStep> steps1 = [
            CreateTestStepMock(name: StepName, propertyBag: new PropertyBag<IPropertyBagValue>()),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: StepName, propertyBag: null),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Removed));
        Assert.That(result.Differences.First().Value1, Is.EqualTo(steps1.First().PropertyBag));
        Assert.That(result.Differences.First().Value2, Is.Null);
        Assert.That(
            result.Differences.First().PropertyName,
            Is.EqualTo($"{nameof(TestStep)}(\"{StepName}\").{nameof(TestStep.PropertyBag)}"));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenPropertyBagIsAbsentInFirstButPresentInSecond()
    {
        const string StepName = "StepName";

        // Arrange
        IList<TestStep> steps1 = [
             CreateTestStepMock(name: StepName, propertyBag: null),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: StepName, propertyBag: new PropertyBag<IPropertyBagValue>()),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Added));
        Assert.That(result.Differences.First().Value1, Is.Null);
        Assert.That(result.Differences.First().Value2, Is.EqualTo(steps2.First().PropertyBag));
        Assert.That(
            result.Differences.First().PropertyName,
            Is.EqualTo($"{nameof(TestStep)}(\"{StepName}\").{nameof(TestStep.PropertyBag)}"));
    }

    [Test]
    public void CompareShouldReturnDiffResultEmptyWhenPropertyBagsAreEqual()
    {
        const string StepName = "StepName";
        var properties = new Dictionary<PropertyBagKey, IPropertyBagValue>
        {
            { PropertyBagKeys.HttpStatus, new PropertyBagValue<string>("value") }
        };

        // Arrange
        IList<TestStep> steps1 = [
             CreateTestStepMock(name: StepName, propertyBag: new PropertyBag<IPropertyBagValue>(properties)),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: StepName, propertyBag: new PropertyBag<IPropertyBagValue>(properties)),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result, Is.EqualTo(DiffResult.Empty));
    }

    [Test]
    public void CompareShouldReturnDiffResultEmptyWhenPropertyBagsHasDifferentKeys()
    {
        const string StepName = "StepName";
        var properties1 = new Dictionary<PropertyBagKey, IPropertyBagValue>
        {
            { PropertyBagKeys.HttpStatus, new PropertyBagValue<string>("value") }
        };

        var properties2 = new Dictionary<PropertyBagKey, IPropertyBagValue>
        {
            { PropertyBagKeys.HttpMethod, new PropertyBagValue<string>("value") }
        };

        // Arrange
        IList<TestStep> steps1 = [
             CreateTestStepMock(name: StepName, propertyBag: new PropertyBag<IPropertyBagValue>(properties1)),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: StepName, propertyBag: new PropertyBag<IPropertyBagValue>(properties2)),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(2));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Removed));
        Assert.That(result.Differences.First().Value1, Is.EqualTo(properties1.First().Value));
        Assert.That(result.Differences.First().Value2, Is.Null);
        Assert.That(
            result.Differences.First().PropertyName,
            Is.EqualTo($"{nameof(TestStep)}(\"{StepName}\").{nameof(TestStep.PropertyBag)}" +
            $"[\"{nameof(PropertyBagKeys.HttpStatus)}\"]"));

        Assert.That(result.Differences.Last().Type, Is.EqualTo(DifferenceType.Added));
        Assert.That(result.Differences.Last().Value1, Is.Null);
        Assert.That(result.Differences.Last().Value2, Is.EqualTo(properties2.First().Value));
        Assert.That(
            result.Differences.Last().PropertyName,
            Is.EqualTo($"{nameof(TestStep)}(\"{StepName}\").{nameof(TestStep.PropertyBag)}" +
            $"[\"{nameof(PropertyBagKeys.HttpMethod)}\"]"));
    }

    [Test]
    public void CompareShouldReturnDiffResultEmptyWhenPropertyBagsHasDifferentValues()
    {
        const string StepName = "StepName";
        PropertyBagKey key = PropertyBagKeys.HttpStatus;

        var properties1 = new Dictionary<PropertyBagKey, IPropertyBagValue>
        {
            { key, new PropertyBagValue<string>("200") }
        };

        var properties2 = new Dictionary<PropertyBagKey, IPropertyBagValue>
        {
            { key, new PropertyBagValue<string>("301") }
        };

        // Arrange
        IList<TestStep> steps1 = [
             CreateTestStepMock(name: StepName, propertyBag: new PropertyBag<IPropertyBagValue>(properties1)),
        ];

        IList<TestStep> steps2 = [
            CreateTestStepMock(name: StepName, propertyBag: new PropertyBag<IPropertyBagValue>(properties2)),
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps1);
        using TestSession session2 = CreateTestSessionMock(steps: steps2);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(1));
        Assert.That(result.Differences.First().Type, Is.EqualTo(DifferenceType.Changed));
        Assert.That(result.Differences.First().Value1, Is.EqualTo(properties1.First().Value));
        Assert.That(result.Differences.First().Value2, Is.EqualTo(properties2.First().Value));
        Assert.That(
            result.Differences.First().PropertyName,
            Is.EqualTo($"{nameof(TestStep)}(\"{StepName}\").{nameof(TestStep.PropertyBag)}[\"{key}\"]"));
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenStepsAreAbsentInFirstButPresentInSecond()
    {
        // Arrange
        IList<TestStep> steps = [
            CreateTestStepMock(),
            CreateTestStepMock(),
            CreateTestStepMock()
        ];

        using TestSession session1 = CreateTestSessionMock(steps: null);
        using TestSession session2 = CreateTestSessionMock(steps: steps);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(steps.Count));

        int i = 0;
        foreach (Difference diff in result.Differences)
        {
            Assert.That(diff.Type, Is.EqualTo(DifferenceType.Added));
            Assert.That(diff.Value1, Is.Null);
            Assert.That(diff.Value2, Is.EqualTo(steps[i++]));
        }
    }

    [Test]
    public void CompareShouldReturnDiffResultWhenStepsAreAbsentInSecondButPresentInFirst()
    {
        // Arrange
        IList<TestStep> steps = [
            CreateTestStepMock(),
            CreateTestStepMock(),
            CreateTestStepMock()
        ];

        using TestSession session1 = CreateTestSessionMock(steps: steps);
        using TestSession session2 = CreateTestSessionMock(steps: null);

        // Act
        TestStepsComparer comparer = CreateComparer();
        DiffResult result = comparer.Compare(session1, session2);

        // Assert
        Assert.That(result.Differences, Has.Count.EqualTo(steps.Count));

        int i = 0;
        foreach (Difference diff in result.Differences)
        {
            Assert.That(diff.Type, Is.EqualTo(DifferenceType.Removed));
            Assert.That(diff.Value1, Is.EqualTo(steps[i++]));
            Assert.That(diff.Value2, Is.Null);
        }
    }
}
