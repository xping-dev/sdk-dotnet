using Xping.Sdk.Actions.Internals;

namespace Xping.Sdk.Availability.UnitTests.Actions.Internals;

public sealed class OrderedUrlRedirectionsTests
{
    [Test]
    public void AddAcceptsNullValue()
    {
        // Arrange
        OrderedUrlRedirections redirections = [];

        // Act
        bool result = redirections.Add(null);

        // Assert
        Assert.That(redirections.Count, Is.EqualTo(0));
        Assert.That(result, Is.EqualTo(false));
    }

    [Test]
    public void AddReturnsTrueWhenUrlInserted()
    {
        // Arrange
        OrderedUrlRedirections redirections = [];

        // Act
        bool result = redirections.Add("http://localhost");

        // Assert
        Assert.That(redirections.Count, Is.EqualTo(1));
        Assert.That(result, Is.EqualTo(true));
    }

    [Test]
    public void AddDoesNotInsertDuplicatedUrls()
    {
        // Arrange
        OrderedUrlRedirections redirections = [];

        // Act
        redirections.Add("http://localhost");
        bool result = redirections.Add("http://localhost");

        // Assert
        Assert.That(redirections.Count, Is.EqualTo(1));
        Assert.That(result, Is.EqualTo(false));
    }

    [Test]
    public void AddPreservesUrlInsertionOrder()
    {
        // Arrange
        List<string> urls = ["http://localhost/A", "http://localhost/B", "http://localhost/C"];
        OrderedUrlRedirections redirections = [];

        // Act
        redirections.Add(urls[0]);
        redirections.Add(urls[1]);
        redirections.Add(urls[2]);

        // Assert
        int index = 0;
        foreach (var url in redirections)
        {
            Assert.That(url, Is.EqualTo(urls[index++]));
        }
    }

    [Test]
    public void FindLastMatchingItemReturnsNullWhenNoItems()
    {
        // Arrange
        OrderedUrlRedirections redirections = [];

        // Act
        var result = redirections.FindLastMatchingItem(url => url != null);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void FindLastMatchingItemReturnsNullWhenNotMatched()
    {
        // Arrange
        OrderedUrlRedirections redirections = ["http://localhost/A", "http://localhost/B"];

        // Act
        var result = redirections.FindLastMatchingItem(url => url == "http://localhost/C");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void FindLastMatchingItemReturnsLastItemWhenMatched()
    {
        // Arrange
        OrderedUrlRedirections redirections = ["http://localhost/A", "http://localhost/B"];

        // Act
        var result = redirections.FindLastMatchingItem(url => url != null);

        // Assert
        Assert.That(result, Is.EqualTo("http://localhost/B"));
    }

    [Test]
    public void ClearRemovesAllItems()
    {
        // Arrange
        OrderedUrlRedirections redirections = ["http://localhost/A", "http://localhost/B"];

        // Act
        redirections.Clear();

        // Assert
        Assert.That(redirections.Count, Is.EqualTo(0));
    }
}
