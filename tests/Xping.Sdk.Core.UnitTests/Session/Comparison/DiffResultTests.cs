using Xping.Sdk.Core.Session.Comparison;

namespace Xping.Sdk.Core.UnitTests.Session.Comparison;

public sealed class DiffResultTests
{
    [Test]
    public void DiffResultEmptyIsEqualToDiffResultEmpty()
    {
        // Assert
        Assert.That(DiffResult.Empty == DiffResult.Empty, Is.True);
    }

    [Test]
    public void NewlyInstantiatedDiffResultIsEqualDiffResultEmpty()
    {
        // Arrange
        var diffResult = new DiffResult();

        // Assert
        Assert.That(diffResult == DiffResult.Empty, Is.True);
    }

    [Test]
    public void NewlyInstantiatedDiffResultHasEmptyDifferences()
    {
        // Arrange
        var diffResult = new DiffResult();

        // Assert
        Assert.That(diffResult.Differences, Has.Count.EqualTo(0));
    }

    [Test]
    public void NewlyInstantiatedDiffResultContainsDiffResultsFromOtherDiffResultsWhenAdded()
    {
        // Arrange
        const int expectedNumberOfDifferences = 2;
        const string propertyName1 = nameof(propertyName1);
        const string propertyName2 = nameof(propertyName2);

        var diffResult1 = new DiffResult();
        diffResult1.AddDifference(new Difference(propertyName1, 1, 2, DifferenceType.Changed));

        var diffResult2 = new DiffResult();
        diffResult2.AddDifference(new Difference(propertyName2, 1, 2, DifferenceType.Changed));

        var diffResult3 = new DiffResult();

        // Act
        diffResult3.Add(diffResult1);
        diffResult3.Add(diffResult2);

        // Assert
        Assert.That(diffResult3.Differences, Has.Count.EqualTo(expectedNumberOfDifferences));
        Assert.That(diffResult3.Differences.First().PropertyName, Is.EqualTo(propertyName1));
        Assert.That(diffResult3.Differences.Last().PropertyName, Is.EqualTo(propertyName2));
    }

    [Test]
    public void AddMethodDoesNotThrowWhenNullResultsAreAdded()
    {
        // Arrange
        var diffResult = new DiffResult();

        // Assert
        Assert.DoesNotThrow(() => diffResult.Add(null!));
    }

    [Test]
    public void AddMethodAllowsToAddDifferencesWithDuplicatedPropertyNames()
    {
        // Arrange
        const int expectedNumberOfDifferences = 2;
        const string propertyName = nameof(propertyName);

        var diffResult1 = new DiffResult();
        diffResult1.AddDifference(new Difference(propertyName, 1, 2, DifferenceType.Changed));

        var diffResult2 = new DiffResult();
        diffResult2.AddDifference(new Difference(propertyName, 1, 2, DifferenceType.Changed));

        // Act
        diffResult1.Add(diffResult2);

        // Assert
        Assert.That(diffResult1.Differences, Has.Count.EqualTo(expectedNumberOfDifferences));
        Assert.That(diffResult1.Differences.First().PropertyName, Is.EqualTo(propertyName));
        Assert.That(diffResult1.Differences.Last().PropertyName, Is.EqualTo(propertyName));
    }

    [Test]
    public void AddMethodDoesNothingWhenResultsFromTheSameInstanceAreAdded()
    {
        // Arrange
        const int expectedNumberOfDifferences = 1;
        const string propertyName = nameof(propertyName);

        var diffResult1 = new DiffResult();
        diffResult1.AddDifference(new Difference(propertyName, 1, 2, DifferenceType.Changed));

        // Act
        diffResult1.Add(diffResult1);

        // Assert
        Assert.That(diffResult1.Differences, Has.Count.EqualTo(expectedNumberOfDifferences));
    }

    [Test]
    public void DiffResultsAreEqualWhenDifferencesAreEqualInValues()
    {
        // Arrange
        const string propertyName = nameof(propertyName);

        var diffResult1 = new DiffResult();
        diffResult1.AddDifference(new Difference(propertyName, 1, 2, DifferenceType.Changed));

        var diffResult2 = new DiffResult();
        diffResult2.AddDifference(new Difference(propertyName, 1, 2, DifferenceType.Changed));

        // Assert
        Assert.That(diffResult1 == diffResult2, Is.True);
    }

    [Test]
    public void DiffResultsAreNotEqualWhenDifferencesAreNotEqualInValues()
    {
        // Arrange
        const string propertyName = nameof(propertyName);

        var diffResult1 = new DiffResult();
        diffResult1.AddDifference(new Difference(propertyName, 1, 2, DifferenceType.Changed));

        var diffResult2 = new DiffResult();
        diffResult2.AddDifference(new Difference(propertyName, 3, 4, DifferenceType.Changed));

        // Assert
        Assert.That(diffResult1 == diffResult2, Is.False);
    }
}
