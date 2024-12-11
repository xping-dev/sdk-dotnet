using HtmlAgilityPack;
using Xping.Sdk.UnitTests.Helpers;
using Xping.Sdk.Validations.Content.Html.Internals;

namespace Xping.Sdk.Availability.UnitTests.Validations.Content.Html.Internals;

public sealed class HtmlNodeIteratorTests
{
    [Test]
    public void CtorSetsDefaultValues()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 0);
        
        // Act
        var iterator = new HtmlNodeIterator(nodes);

        // Assert
        Assert.That(iterator.Cursor, Is.EqualTo(-1));
        Assert.That(iterator.IsAdvanced, Is.False);
    }

    [Test]
    public void CtorShoulThrowArgumentNullExceptionWhenNodesAreNull()
    {
        // Arrange
        HtmlNodeCollection nodes = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new HtmlNodeIterator(nodes));
    }

    [Test]
    public void FirstShouldSetCursorToZeroWhenNodesNotEmpty()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 1);
        var iterator = new HtmlNodeIterator(nodes);

        // Act
        iterator.First();

        // Assert
        Assert.That(iterator.Cursor == 0, "Cursor should be set to 0 after calling First.");
        Assert.That(iterator.IsAdvanced, Is.True, "Iterator should be advanced after calling Frist.");
    }

    [Test]
    public void FirstShouldNotSetCursorToZeroWhenNodesAreEmpty()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 0);
        var iterator = new HtmlNodeIterator(nodes);

        // Act
        iterator.First();

        // Assert
        Assert.That(iterator.Cursor != 0, "Cursor should not be set to 0 after calling First.");
        Assert.That(iterator.IsAdvanced, Is.False, "Iterator should not be advanced after calling Frist.");
    }

    [Test]
    public void LastShouldSetCursorToLastElementWhenNodesNotEmpty()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 2);
        var iterator = new HtmlNodeIterator(nodes);

        // Act
        iterator.Last();

        // Assert
        Assert.That(iterator.Cursor == 1, "Cursor should be set to 1 after calling Last.");
        Assert.That(iterator.IsAdvanced, Is.True, "Iterator should be advanced after calling Last.");
    }

    [Test]
    public void LastShouldNotSetCursorToLastElementWhenNodesAreEmpty()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 0);
        var iterator = new HtmlNodeIterator(nodes);

        // Act
        iterator.Last();

        // Assert
        Assert.That(iterator.Cursor != 1, "Cursor should not be set to 1 after calling Last.");
        Assert.That(iterator.IsAdvanced, Is.False, "Iterator should not be advanced after calling Last.");
    }

    [Test]
    public void NthShouldSetCursorToNthElementWhenNodesNotEmpty()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var iterator = new HtmlNodeIterator(nodes);

        // Act
        iterator.Nth(1);

        // Assert
        Assert.That(iterator.Cursor == 1, "Cursor should be set to 1 after calling Nth.");
        Assert.That(iterator.IsAdvanced, Is.True, "Iterator should be advanced after calling Nth.");
    }

    [Test]
    public void NthShouldSetCursorToFirstElementWhenNodesNotEmpty()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var iterator = new HtmlNodeIterator(nodes);

        // Act
        iterator.Nth(0);

        // Assert
        Assert.That(iterator.Cursor == 0, "Cursor should be set to 0 after calling Nth.");
        Assert.That(iterator.IsAdvanced, Is.True, "Iterator should be advanced after calling Nth.");
    }

    [Test]
    public void NthShouldNotSetCursorToNthElementWhenNodesAreEmpty()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 0);
        var iterator = new HtmlNodeIterator(nodes);

        // Act
        iterator.Nth(1);

        // Assert
        Assert.That(iterator.Cursor != 1, "Cursor should not be set to 1 after calling Nth.");
        Assert.That(iterator.IsAdvanced, Is.False, "Iterator should not be advanced after calling Nth.");
    }

    [Test]
    public void NthShouldNotSetCursorToNthElementWhenIndexOutOfRange()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        int outOfRangeIndex = nodes.Count + 2;
        var iterator = new HtmlNodeIterator(nodes);

        // Act
        iterator.Nth(outOfRangeIndex);

        // Assert
        Assert.That(iterator.Cursor != outOfRangeIndex, $"Cursor should not be set to {outOfRangeIndex}.");
        Assert.That(iterator.IsAdvanced, Is.False, "Iterator should not be advanced after calling Nth.");
    }

    [Test]
    public void CurrentReturnsNullWhenNodesAreEmpty()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 0);
        var iterator = new HtmlNodeIterator(nodes);

        // Act
        var result = iterator.Current();

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void CurrentThrowsExceptionWhenCursorNotAdvanced()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var iterator = new HtmlNodeIterator(nodes);

        // Act && Assert
        Assert.Throws<InvalidOperationException>(() => iterator.Current());
    }

    [Test]
    public void CurrentReturnsFirstElementWhenFirstCalled()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var iterator = new HtmlNodeIterator(nodes);

        // Act 
        iterator.First();
        var result = iterator.Current();

        // Assert
        Assert.That(result, Is.EqualTo(nodes.ElementAt(0)));
    }
}
