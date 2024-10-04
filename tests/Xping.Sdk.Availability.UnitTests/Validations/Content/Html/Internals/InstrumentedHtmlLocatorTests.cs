using System.Xml.XPath;
using HtmlAgilityPack;
using Moq;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Session;
using Xping.Sdk.UnitTests.Helpers;
using Xping.Sdk.Validations;
using Xping.Sdk.Validations.Content.Html.Internals;
using Xping.Sdk.Validations.TextUtils;
using TestContext = Xping.Sdk.Core.Components.TestContext;

namespace Xping.Sdk.Availability.UnitTests.Validations.Content.Html.Internals;

public sealed class InstrumentedHtmlLocatorTests : XpingAssertions
{
    private readonly TestContext _testContext = HtmlContentTestsHelpers.CreateTestContext();
    private Mock<IIterator<HtmlNode>> _iteratorMock;

    [SetUp]
    public void SetUp()
    {
        _iteratorMock = new Mock<IIterator<HtmlNode>>();
    }

    [Test]
    public void CtorThrowsArgumentNullExceptionWhenNodesAreNull()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new InstrumentedHtmlLocator(
                nodes: null!,
                iterator: _iteratorMock.Object,
                context: _testContext);
        });
    }

    [Test]
    public void CtorThrowsArgumentNullExceptionWhenIteratorIsNull()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new InstrumentedHtmlLocator(
                nodes: HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 0),
                iterator: null!,
                context: _testContext);
        });
    }

    [Test]
    public void CtorThrowsArgumentNullExceptionWhenContextIsNull()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new InstrumentedHtmlLocator(
                nodes: HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 0),
                iterator: _iteratorMock.Object,
                context: null!);
        });
    }

    [Test]
    public void CtorDoesNotAdvanceIteratorWhenNodesAreEmpty()
    {
        // Arrange & Act
        _ = new InstrumentedHtmlLocator(
            nodes: HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 0),
            iterator: _iteratorMock.Object,
            context: HtmlContentTestsHelpers.CreateTestContext());

        // Assert
        _iteratorMock.Verify(i => i.First(), Times.Never);
        _iteratorMock.Verify(i => i.Last(), Times.Never);
        _iteratorMock.Verify(i => i.Nth(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void CtorAdvancesIteratorByCallingFirstWhenNodesAreNotEmpty()
    {
        // Arrange & Act
        _ = new InstrumentedHtmlLocator(
            nodes: HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3),
            iterator: _iteratorMock.Object,
            context: HtmlContentTestsHelpers.CreateTestContext());

        // Assert
        _iteratorMock.Verify(i => i.First(), Times.Once);
        _iteratorMock.Verify(i => i.Last(), Times.Never);
        _iteratorMock.Verify(i => i.Nth(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public void FirstBuildsTestSession()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());

        // Act
        htmlLocator.First();

        // Assert
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(htmlLocator.First))))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string[]>(nodes.Select(n => n.OriginalName.Trim()).ToArray())))),
                        Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("CurrentNode")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nodes.First().OriginalName.Trim())))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void FirstAdvancesToFirstItem()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var iterator = new HtmlNodeIterator(nodes);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: iterator,
            context: HtmlContentTestsHelpers.CreateTestContext());

        // Act
        htmlLocator.First();

        // Assert
        Assert.That(iterator.IsAdvanced, Is.True);
        Assert.That(iterator.Current(), Is.Not.Null);
        Mock.Get(htmlLocator.Context.SessionBuilder)
           .Verify(m => m.Build(
               It.Is<PropertyBagKey>(p => p == new PropertyBagKey("CurrentNode")),
               It.Is<IPropertyBagValue>(p =>
                   p.Equals(new PropertyBagValue<string>(nodes.First().OriginalName.Trim())))), Times.Once);
    }

    [Test]
    public void FirstDoesNotAdvanceToFirstItemWhenNodesEmpty()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 0);
        var iterator = new HtmlNodeIterator(nodes);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: iterator,
            context: HtmlContentTestsHelpers.CreateTestContext());

        // Act
        htmlLocator.First();

        // Assert
        Assert.That(iterator.IsAdvanced, Is.False);
        Assert.That(iterator.Current(), Is.Null);
        Mock.Get(htmlLocator.Context.SessionBuilder)
           .Verify(m => m.Build(
               It.Is<PropertyBagKey>(p => p == new PropertyBagKey("CurrentNode")),
               It.Is<IPropertyBagValue>(p =>
                   p.Equals(new PropertyBagValue<string>("Null")))), Times.Once);
    }

    [Test]
    public void FirstInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: _iteratorMock.Object,
            context: HtmlContentTestsHelpers.CreateTestContext());

        // Act
        htmlLocator.First();

        // Assert
        Mock.Get(htmlLocator.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }

    [Test]
    public void LastBuildsTestSession()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());

        // Act
        htmlLocator.Last();

        // Assert
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(htmlLocator.Last))))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string[]>(nodes.Select(n => n.OriginalName.Trim()).ToArray())))),
                        Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("CurrentNode")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nodes.Last().OriginalName.Trim())))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void LastAdvancesToLastItem()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: _iteratorMock.Object,
            context: HtmlContentTestsHelpers.CreateTestContext());

        // Act
        htmlLocator.Last();

        // Assert
        _iteratorMock.Verify(m => m.Last(), Times.Once);
    }

    [Test]
    public void LastDoesNotAdvanceToLastItemWhenNodesEmpty()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 0);
        var iterator = new HtmlNodeIterator(nodes);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: iterator,
            context: HtmlContentTestsHelpers.CreateTestContext());

        // Act
        htmlLocator.Last();

        // Assert
        Assert.That(iterator.IsAdvanced, Is.False);
        Assert.That(iterator.Current(), Is.Null);
        Mock.Get(htmlLocator.Context.SessionBuilder)
           .Verify(m => m.Build(
               It.Is<PropertyBagKey>(p => p == new PropertyBagKey("CurrentNode")),
               It.Is<IPropertyBagValue>(p =>
                   p.Equals(new PropertyBagValue<string>("Null")))), Times.Once);
    }

    [Test]
    public void LastInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: _iteratorMock.Object,
            context: HtmlContentTestsHelpers.CreateTestContext());

        // Act
        htmlLocator.Last();

        // Assert
        Mock.Get(htmlLocator.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }

    [Test]
    public void FilterBuildsTestSessionWithEmptyNodes()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 0);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());
        var options = new FilterOptions { HasText = "Password" };
        // Act
        htmlLocator.Filter(options);

        // Assert
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(htmlLocator.Filter))))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("CurrentNode")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>("Null")))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("FilterOptions")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(options.ToString())))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("ChildNodes")),
                It.IsAny<IPropertyBagValue>()), Times.Never);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("FilteredNodes")),
                It.IsAny<IPropertyBagValue>()), Times.Never);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void FilterBuildsTestSessionWithNotEmptyNodes()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());
        var options = new FilterOptions { HasText = "Password" };
        htmlLocator.First();
        Mock.Get(htmlLocator.Context.SessionBuilder).Invocations.Clear();

        // Act
        htmlLocator.Filter(options);

        // Assert
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(htmlLocator.Filter))))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("CurrentNode")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>("label")))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("FilterOptions")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(options.ToString())))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("ChildNodes")),
                It.IsAny<IPropertyBagValue>()), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("FilteredNodes")),
                It.IsAny<IPropertyBagValue>()), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void FilterReturnsNewObject()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());
        var options = new FilterOptions { HasText = "Password" };

        htmlLocator.First();

        // Act
        var newLocator = htmlLocator.Filter(options);

        // Assert
        Assert.That(newLocator, Is.Not.EqualTo(htmlLocator));
    }

    [Test]
    public void FilterReturnsItSelfWhenNoResults()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());
        var options = new FilterOptions { HasText = "NotFoundNode" };

        htmlLocator.First();

        // Act
        var newLocator = htmlLocator.Filter(options);

        // Assert
        Assert.That(newLocator, Is.EqualTo(htmlLocator));
    }

    [Test]
    public void FilterInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());
        var options = new FilterOptions { HasText = "Password" };

        htmlLocator.First();
        Mock.Get(htmlLocator.Context.Progress!).Invocations.Clear();

        // Act
        _ = htmlLocator.Filter(options);

        // Assert
        Mock.Get(htmlLocator.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }

    [Test]
    public void LocatorBuildsTestSessionWithEmptyNodes()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 0);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());
        
        // Act
        htmlLocator.Locate(XPathExpression.Compile("//label"));

        // Assert
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(htmlLocator.Locate))))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("CurrentNode")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>("Null")))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("ChildNodes")),
                It.IsAny<IPropertyBagValue>()), Times.Never);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("FilteredNodes")),
                It.IsAny<IPropertyBagValue>()), Times.Never);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void LocatorBuildsTestSessionWithNotEmptyNodes()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());
        htmlLocator.First();
        Mock.Get(htmlLocator.Context.SessionBuilder).Invocations.Clear();

        // Act
        htmlLocator.Locate(XPathExpression.Compile("//label"));

        // Assert
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(htmlLocator.Locate))))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("CurrentNode")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>("label")))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("selector")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>("//label")))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("ChildNodes")),
                It.IsAny<IPropertyBagValue>()), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("LocatedNodes")),
                It.IsAny<IPropertyBagValue>()), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void LocatorReturnsNewObject()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());
        htmlLocator.First();

        // Act
        var newLocator = htmlLocator.Locate(XPathExpression.Compile("//label"));

        // Assert
        Assert.That(newLocator, Is.Not.EqualTo(htmlLocator));
    }

    [Test]
    public void LocatorReturnsItSelfWhenNoResults()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());
        htmlLocator.First();

        // Act
        var newLocator = htmlLocator.Locate(XPathExpression.Compile("//notfound"));

        // Assert
        Assert.That(newLocator, Is.EqualTo(htmlLocator));
    }

    [Test]
    public void LocatorInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());

        htmlLocator.First();
        Mock.Get(htmlLocator.Context.Progress!).Invocations.Clear();

        // Act
        _ = htmlLocator.Locate(XPathExpression.Compile("//label"));

        // Assert
        Mock.Get(htmlLocator.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }

    [Test]
    public void NthThrowsExceptionWhenIndexIsNegative()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => htmlLocator.Nth(-1));
        Assert.That(exception.Message, Is.EqualTo("Index must be a positive integer."));
    }

    [Test]
    public void NthThrowsExceptionWhenIndexIsGreaterThenCollectionCount()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => htmlLocator.Nth(5));
        Assert.That(exception.Message, Is.EqualTo(
            $"Expected to access the 5th index, but only 3 elements exist. This error occurred as part of validating " +
            $"HTML data."));
    }

    [Test]
    public void NthThrowsExceptionWhenCollectionIsEmpty()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 0);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => htmlLocator.Nth(0));
        Assert.That(exception.Message, Is.EqualTo(
            $"Expected to access the 0th index, but only 0 elements exist. This error occurred as part of validating " +
            $"HTML data."));
    }

    [Test]
    public void NthBuildsTestSession()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());
        var index = 1;
        // Act
        htmlLocator.Nth(index);

        // Assert
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(htmlLocator.Nth))))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("index")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>($"{index}")))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string[]>(nodes.Select(n => n.OriginalName.Trim()).ToArray())))),
                        Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("CurrentNode")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nodes.Last().OriginalName.Trim())))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void NthInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());

        // Act
        _ = htmlLocator.Nth(index: 1);

        // Assert
        Mock.Get(htmlLocator.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }

    [Test]
    public void HasCountBuildsTestSessionWhenPassedValidation()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());
        var count = nodes.Count;
        
        // Act
        Expect(htmlLocator).ToHaveCount(count);

        // Assert
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(HtmlLocatorAssertions.ToHaveCount))))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("expectedCount")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>($"{count}")))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string[]>(nodes.Select(n => n.OriginalName.Trim()).ToArray())))),
                        Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void HasCountBuildsTestSessionWhenNotPassedValidation()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());
        var count = nodes.Count + 2;

        // Act
        Assert.Throws<ValidationException>(() => Expect(htmlLocator).ToHaveCount(count));

        // Assert
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(HtmlLocatorAssertions.ToHaveCount))))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("expectedCount")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>($"{count}")))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string[]>(nodes.Select(n => n.OriginalName.Trim()).ToArray())))),
                        Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Never);
    }

    [Test]
    public void HasCountInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());

        // Act
        Expect(htmlLocator).ToHaveCount(expectedCount: 3);

        // Assert
        Mock.Get(htmlLocator.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }

    [Test]
    public void HasInnerTextBuildsTestSessionWhenPassedValidation()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());
        var options = new TextOptions { MatchCase = false, MatchWholeWord = false };
        htmlLocator.First();
        Mock.Get(htmlLocator.Context.SessionBuilder).Invocations.Clear();
        const string innerText = "Password";

        // Act
        Expect(htmlLocator).ToHaveInnerText(innerText, options);

        // Assert
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(
                        new PropertyBagValue<string>(nameof(HtmlLocatorAssertions.ToHaveInnerText))))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("elementInnerTexts")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(innerText)))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("TextOptions")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(options.ToString())))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("CurrentNode")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>("label")))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void HasInnerTextBuildsTestSessionWhenNotPassedValidation()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());
        var options = new TextOptions { MatchCase = false, MatchWholeWord = false };
        htmlLocator.First();
        Mock.Get(htmlLocator.Context.SessionBuilder).Invocations.Clear();
        const string innerText = "notFound";

        // Act
        Assert.Throws<ValidationException>(() => Expect(htmlLocator).ToHaveInnerText(innerText, options));

        // Assert
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(
                        new PropertyBagValue<string>(nameof(HtmlLocatorAssertions.ToHaveInnerText))))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("elementInnerTexts")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(innerText)))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("TextOptions")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(options.ToString())))), Times.Once);
        Mock.Get(htmlLocator.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("CurrentNode")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>("label")))), Times.Once);
    }

    [Test]
    public void HasInnerTextInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var nodes = HtmlContentTestsHelpers.CreateHtmlNodeCollection(count: 3);
        var htmlLocator = new InstrumentedHtmlLocator(
            nodes: nodes,
            iterator: new HtmlNodeIterator(nodes),
            context: HtmlContentTestsHelpers.CreateTestContext());
        htmlLocator.First();
        Mock.Get(htmlLocator.Context.Progress!).Invocations.Clear();

        // Act
        Expect(htmlLocator).ToHaveInnerText(innerText: "Password");

        // Assert
        Mock.Get(htmlLocator.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }
}
