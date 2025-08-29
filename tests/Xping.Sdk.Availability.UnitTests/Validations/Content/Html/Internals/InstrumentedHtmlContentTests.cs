/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using HtmlAgilityPack;
using Moq;
using Xping.Sdk.UnitTests.Helpers;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Validations;
using Xping.Sdk.Validations.Content.Html;
using Xping.Sdk.Validations.Content.Html.Internals;
using Xping.Sdk.Validations.Content.Html.Internals.Selectors;
using Xping.Sdk.Validations.TextUtils;
using TestContext = Xping.Sdk.Core.Components.TestContext;

namespace Xping.Sdk.Availability.UnitTests.Validations.Content.Html.Internals;

public sealed class InstrumentedHtmlContentTests
{
    private readonly TestContext _testContext = HtmlContentTestsHelpers.CreateTestContext();

    private sealed class HtmlAssertionsMock(IHtmlContent content) : HtmlAssertions(content)
    {
        public Mock<ISelector> MockedSelector { get; } = new();

        protected override ISelector CreateXPathSelector(XPath xpath)
        {
            return MockedSelector.Object;
        }
    }

    private sealed class InstrumentedHtmlContentUnderTest(string data, TestContext context, string testIdAttribute) :
        InstrumentedHtmlContent(data, context, testIdAttribute)
    {
        private readonly Mock<ISelector> _mockedSelector = new();

        public Mock<ISelector> MockedSelector => _mockedSelector;

        protected override ISelector CreateByAltTextSelector(string text, TextOptions? options = null)
        {
            return _mockedSelector.Object;
        }

        protected override ISelector CreateByAltRegexSelector(Regex text, TextOptions? options = null)
        {
            return _mockedSelector.Object;
        }
    }

    [Test]
    public void CtorThrowsArgumentExceptionWhenDataIsNull()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new InstrumentedHtmlContent(data: null!, _testContext, "testIdAttribute");
        });
    }

    [Test]
    public void CtorDoesNotThrowWhenDataIsEmpty()
    {
        // Assert
        Assert.DoesNotThrow(() =>
        {
            _ = new InstrumentedHtmlContent(data: string.Empty, _testContext, "testIdAttribute");
        });
    }

    [Test]
    public void CtorThrowsArgumentNullExceptionWhenTestContextIsNull()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new InstrumentedHtmlContent(data: "<html></html>", context: null!, "testIdAttribute");
        });
    }

    [Test]
    public void CtorThrowsArgumentExceptionWhenTestIdAttributeIsNull()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new InstrumentedHtmlContent(data: "<html></html>", _testContext, testIdAttribute: null!);
        });
    }

    [Test]
    public void CtorThrowsArgumentExceptionWhenTestIdAttributeIsEmpty()
    {
        // Assert
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new InstrumentedHtmlContent(data: "<html></html>", _testContext, testIdAttribute: string.Empty);
        });
    }

    [Test]
    public void HasTitleBuildsTestSession()
    {
        // Arrange
        const string expectedTitle = "Title";
        var instrumentedHtml = new InstrumentedHtmlContent(
            HtmlContentTestsHelpers.GetTitleHtml(title: expectedTitle),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var options = new TextOptions() { MatchCase = false, MatchWholeWord = false };
        var expect = new HtmlAssertions(instrumentedHtml);

        // Act
        expect.ToHaveTitle(expectedTitle, options);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(HtmlAssertions.ToHaveTitle))))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("title")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(expectedTitle)))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey(nameof(TextOptions))),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(options.ToString())))), Times.Once);
    }

    [Test]
    public void HasTitleBuildsTestSessionWhenProvidedTextOptionsIsNull()
    {
        // Arrange
        const string expectedTitle = "Title";
        var instrumentedHtml = new InstrumentedHtmlContent(
            HtmlContentTestsHelpers.GetTitleHtml(title: expectedTitle),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var expect = new HtmlAssertions(instrumentedHtml);

        // Act
        expect.ToHaveTitle(expectedTitle, options: null);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey(nameof(TextOptions))),
                It.Is<IPropertyBagValue>(p => p.Equals(new PropertyBagValue<string>("Null")))), Times.Once);
    }

    [Test]
    public void HasTitleBuildsTestSessionWithSelectorResults()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetTitleHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode("<title>expectedTitle</title>"),
        };

        var expect = new HtmlAssertionsMock(instrumentedHtml);
        expect.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        expect.ToHaveTitle("expectedTitle");

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string[]>(
                        htmlNodeCollection.Select(n => n.OriginalName.Trim()).ToArray())))), Times.Once);
    }

    [Test]
    public void HasTitleShouldCreateSuccessfulTestStepWhenCompletedSuccessfully()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetTitleHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode("<title>expectedTitle</title>"),
        };

        var expect = new HtmlAssertionsMock(instrumentedHtml);
        expect.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        expect.ToHaveTitle("expectedTitle");

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder).Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void HasTitleInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetTitleHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode("<title>expectedTitle</title>"),
        };

        var expect = new HtmlAssertionsMock(instrumentedHtml);
        expect.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        expect.ToHaveTitle("expectedTitle");

        // Assert
        Mock.Get(instrumentedHtml.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }

    [Test]
    public void HasTitleThrowsValidationExceptionWhenSelectorReturnsNoNodes()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetTitleHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            // No HtmlNode instance created simulating a collection with zero nodes.
        };

        var expect = new HtmlAssertionsMock(instrumentedHtml);
        expect.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => expect.ToHaveTitle("expectedTitle"));

        Assert.That(exception.Message, Is.EqualTo("Expected <title> node. Actual \"not found\"."));
    }

    [Test]
    public void HasTitleThrowsValidationExceptionWhenSelectorReturnsMoreThanOneNode()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetTitleHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode("<title>expectedTitle</title>"),
            HtmlNode.CreateNode("<title>expectedTitle</title>"),
        };

        var expect = new HtmlAssertionsMock(instrumentedHtml);
        expect.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => expect.ToHaveTitle("expectedTitle"));

        Assert.That(exception.Message, Is.EqualTo("Expected single <title> node. Actual \"2 nodes found\"."));
    }

    [Test]
    public void HasTitleThrowsValidationExceptionWhenTitlesDoesNotMatch()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetTitleHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode("<title>expectedTitle</title>"),
        };

        var expect = new HtmlAssertionsMock(instrumentedHtml);
        expect.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => expect.ToHaveTitle("OtherTitle"));

        Assert.That(exception.Message, Is.EqualTo("Expected <title> node inner text \"OtherTitle\". Actual \"expectedTitle\"."));
    }

    [Test]
    public void HasMaxDocumentSizeBuildsTestSession()
    {
        // Arrange
        int maxSizeInBytes = 150;
        var instrumentedHtml = new InstrumentedHtmlContent(
            HtmlContentTestsHelpers.GetTitleHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var expect = new HtmlAssertions(instrumentedHtml);

        // Act
        expect.ToHaveMaxDocumentSize(maxSizeInBytes);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(
                        nameof(HtmlAssertions.ToHaveMaxDocumentSize))))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                    It.Is<PropertyBagKey>(p => p == new PropertyBagKey("maxSizeInBytes")),
                    It.Is<IPropertyBagValue>(p =>
                        p.Equals(new PropertyBagValue<string>(maxSizeInBytes.ToString(CultureInfo.InvariantCulture))))),
                Times.Once);
    }

    [Test]
    public void HasMaxDocumentSizeBuildsTestSessionWithActualResults()
    {
        // Arrange
        var html = HtmlContentTestsHelpers.GetTitleHtml();
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            html, HtmlContentTestsHelpers.CreateTestContext(), "data-testid");
        var expect = new HtmlAssertions(instrumentedHtml);

        // Act
        expect.ToHaveMaxDocumentSize(maxSizeInBytes: 150);
        var document = instrumentedHtml.Document;
        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                    It.Is<PropertyBagKey>(p => p == new PropertyBagKey("SizeInBytes")),
                    It.Is<IPropertyBagValue>(p =>
                        p.Equals(new PropertyBagValue<string>(
                            document.Encoding.GetByteCount(document.Text).ToString(CultureInfo.InvariantCulture))))),
                Times.Once);
    }

    [Test]
    public void HasMaxDocumentSizeShouldCreateSuccessfulTestStepWhenCompletedSuccessfully()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetTitleHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var expect = new HtmlAssertions(instrumentedHtml);

        // Act
        expect.ToHaveMaxDocumentSize(maxSizeInBytes: 150);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder).Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void HasMaxDocumentSizeInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetTitleHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var expect = new HtmlAssertions(instrumentedHtml);

        // Act
        expect.ToHaveMaxDocumentSize(maxSizeInBytes: 150);

        // Assert
        Mock.Get(instrumentedHtml.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }

    [Test]
    public void HasMaxDocumentSizeThrowsValidationExceptionWhenActualSizeIsGreaterThanMaxSizeAllowed()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetTitleHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var expect = new HtmlAssertions(instrumentedHtml);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => { expect.ToHaveMaxDocumentSize(maxSizeInBytes: 5); });

        var document = instrumentedHtml.Document;
        var byteCount = document.Encoding.GetByteCount(document.Text).ToString(CultureInfo.InvariantCulture);

        Assert.That(exception.Message, Is.EqualTo(
            $"The expected HTML document size should be equal to or less than 5 bytes; however, " +
            $"the actual size was {byteCount} bytes. This error occurred during the validation of HTML data."));
    }

    [Test]
    public void HasMaxDocumentSizeOperatesOnEmptyHtmlDocument()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            data: string.Empty,
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var expect = new HtmlAssertions(instrumentedHtml);

        // Act & Assert
        Assert.DoesNotThrow(() => expect.ToHaveMaxDocumentSize(maxSizeInBytes: 5));
    }

    [Test]
    public void GetByAltTextBuildsTestSession()
    {
        // Arrange
        const string altText = "logo";
        var instrumentedHtml = new InstrumentedHtmlContent(
            HtmlContentTestsHelpers.GetAltHtml(altText),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var options = new TextOptions() { MatchCase = false, MatchWholeWord = false };

        // Act
        instrumentedHtml.GetByAltText(text: altText, options);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(instrumentedHtml.GetByAltText))))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("text")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(altText)))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey(nameof(TextOptions))),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(options.ToString())))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.IsAny<IPropertyBagValue>()), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void GetByAltTextBuildsTestSessionWhenProvidedTextOptionsIsNull()
    {
        // Arrange
        var altText = "logo";
        var instrumentedHtml = new InstrumentedHtmlContent(
            HtmlContentTestsHelpers.GetAltHtml(altText),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");

        // Act
        instrumentedHtml.GetByAltText(text: altText, options: null);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey(nameof(TextOptions))),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>("Null")))), Times.Once);
    }

    [Test]
    public void GetByAltTextBuildsTestSessionWithSelectorResults()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetAltHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<img alt=\"logo\" src=\"/img/xping365-logo.svg\" width=\"100\" />"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByAltText(text: "logo");

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string[]>(
                        htmlNodeCollection.Select(n => n.OriginalName.Trim()).ToArray())))), Times.Once);
    }

    [Test]
    public void GetByAltTextShouldCreateSuccessfulTestStepWhenCompletedSuccessfully()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetAltHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<img alt=\"logo\" src=\"/img/xping365-logo.svg\" width=\"100\" />"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByAltText(text: "logo");

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder).Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void GetByAltTextInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetAltHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<img alt=\"logo\" src=\"/img/xping365-logo.svg\" width=\"100\" />"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByAltText(text: "logo");

        // Assert
        Mock.Get(instrumentedHtml.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }

    [Test]
    public void GetByAltTextRegexBuildsTestSession()
    {
        // Arrange
        var regex = new Regex("[a-z]");
        var instrumentedHtml = new InstrumentedHtmlContent(
            HtmlContentTestsHelpers.GetAltHtml("alt-text"),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");

        // Act
        instrumentedHtml.GetByAltText(text: regex);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(instrumentedHtml.GetByAltText))))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("text")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(regex.ToString())))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.IsAny<IPropertyBagValue>()), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void GetByAltTextRegexBuildsTestSessionWithSelectorResults()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetAltHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<img alt=\"logo\" src=\"/img/xping365-logo.svg\" width=\"100\" />"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByAltText(text: new Regex("[a-z]"));

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string[]>(
                        htmlNodeCollection.Select(n => n.OriginalName.Trim()).ToArray())))), Times.Once);
    }

    [Test]
    public void GetByAltTextRegexShouldCreateSuccessfulTestStepWhenCompletedSuccessfully()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetAltHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<img alt=\"logo\" src=\"/img/xping365-logo.svg\" width=\"100\" />"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByAltText(text: new Regex("[a-z]"));

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder).Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void GetByAltTextRegexInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetAltHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<img alt=\"logo\" src=\"/img/xping365-logo.svg\" width=\"100\" />"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByAltText(text: new Regex("[a-z]"));

        // Assert
        Mock.Get(instrumentedHtml.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }

    [Test]
    public void GetByLabelBuildsTestSession()
    {
        // Arrange
        var label = "password";
        var instrumentedHtml = new InstrumentedHtmlContent(
            HtmlContentTestsHelpers.GetLabelHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var options = new TextOptions() { MatchCase = false, MatchWholeWord = false };

        // Act
        instrumentedHtml.GetByLabel(text: label, options);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(instrumentedHtml.GetByLabel))))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("text")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(label)))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey(nameof(TextOptions))),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(options.ToString())))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.IsAny<IPropertyBagValue>()), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void GetByLabelBuildsTestSessionWhenProvidedTextOptionsIsNull()
    {
        // Arrange
        var label = "password";
        var instrumentedHtml = new InstrumentedHtmlContent(
            data: HtmlContentTestsHelpers.GetLabelHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");

        // Act
        instrumentedHtml.GetByLabel(text: label, options: null);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey(nameof(TextOptions))),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>("Null")))), Times.Once);
    }

    [Test]
    public void GetByLabelBuildsTestSessionWithSelectorResults()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetLabelHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<label>Password <input type=\"password\" /></label>"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByLabel(text: "password");

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string[]>(
                        htmlNodeCollection.Select(n => n.OriginalName.Trim()).ToArray())))), Times.Once);
    }

    [Test]
    public void GetByLabelShouldCreateSuccessfulTestStepWhenCompletedSuccessfully()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetLabelHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<label>Password <input type=\"password\" /></label>"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByLabel(text: "password");

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder).Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void GetByLabelInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetLabelHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<label>Password <input type=\"password\" /></label>"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByLabel(text: "password");

        // Assert
        Mock.Get(instrumentedHtml.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }

    [Test]
    public void GetByPlaceholderBuildsTestSession()
    {
        // Arrange
        var text = "name@example.com";
        var instrumentedHtml = new InstrumentedHtmlContent(
            data: HtmlContentTestsHelpers.GetPlaceholderHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");
        var options = new TextOptions() { MatchCase = false, MatchWholeWord = false };

        // Act
        instrumentedHtml.GetByPlaceholder(text: text, options);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(instrumentedHtml.GetByPlaceholder))))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("text")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(text)))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey(nameof(TextOptions))),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(options.ToString())))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.IsAny<IPropertyBagValue>()), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void GetByPlaceholderBuildsTestSessionWhenProvidedTextOptionsIsNull()
    {
        // Arrange
        var text = "name@example.com";
        var instrumentedHtml = new InstrumentedHtmlContent(
            data: HtmlContentTestsHelpers.GetPlaceholderHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");

        // Act
        instrumentedHtml.GetByPlaceholder(text: text, options: null);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey(nameof(TextOptions))),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>("Null")))), Times.Once);
    }

    [Test]
    public void GetByPlaceholderBuildsTestSessionWithSelectorResults()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            data: HtmlContentTestsHelpers.GetPlaceholderHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<input type=\"email\" placeholder=\"name@example.com\" />"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByPlaceholder(text: "name@example.com");

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string[]>(
                        htmlNodeCollection.Select(n => n.OriginalName.Trim()).ToArray())))), Times.Once);
    }

    [Test]
    public void GetByPlaceholderShouldCreateSuccessfulTestStepWhenCompletedSuccessfully()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetPlaceholderHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<input type=\"email\" placeholder=\"name@example.com\" />"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByPlaceholder(text: "name@example.com");

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder).Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void GetByPlaceholderInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            HtmlContentTestsHelpers.GetPlaceholderHtml(),
            HtmlContentTestsHelpers.CreateTestContext(),
            "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<input type=\"email\" placeholder=\"name@example.com\" />"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByAltText(text: "password");

        // Assert
        Mock.Get(instrumentedHtml.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }

    [Test]
    public void GetByTestIdBuildsTestSession()
    {
        // Arrange
        var text = "directions";
        var instrumentedHtml = new InstrumentedHtmlContent(
            data: HtmlContentTestsHelpers.GetTestIdHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");
        var options = new TextOptions() { MatchCase = false, MatchWholeWord = false };

        // Act
        instrumentedHtml.GetByTestId(text, options);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(instrumentedHtml.GetByTestId))))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("text")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(text)))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey(nameof(TextOptions))),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(options.ToString())))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.IsAny<IPropertyBagValue>()), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void GetByTestIdBuildsTestSessionWhenProvidedTextOptionsIsNull()
    {
        // Arrange
        var text = "directions";
        var instrumentedHtml = new InstrumentedHtmlContent(
            data: HtmlContentTestsHelpers.GetTestIdHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");

        // Act
        instrumentedHtml.GetByTestId(text, options: null);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey(nameof(TextOptions))),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>("Null")))), Times.Once);
    }

    [Test]
    public void GetByTestIdBuildsTestSessionWithSelectorResults()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            data: HtmlContentTestsHelpers.GetTestIdHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<button data-test-id=\"directions\">Itinerary</button>"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByTestId(text: "directions");

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string[]>(
                        htmlNodeCollection.Select(n => n.OriginalName.Trim()).ToArray())))), Times.Once);
    }

    [Test]
    public void GetByTestIdShouldCreateSuccessfulTestStepWhenCompletedSuccessfully()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            data: HtmlContentTestsHelpers.GetTestIdHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<button data-test-id=\"directions\">Itinerary</button>"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByTestId(text: "data-testid");

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder).Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void GetByTestIdInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            data: HtmlContentTestsHelpers.GetTestIdHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<button data-test-id=\"directions\">Itinerary</button>"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByTestId(text: "data-testid");

        // Assert
        Mock.Get(instrumentedHtml.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }

    [Test]
    public void GetByTitleBuildsTestSession()
    {
        // Arrange
        var text = "Issues count";
        var instrumentedHtml = new InstrumentedHtmlContent(
            data: HtmlContentTestsHelpers.GetTitleAttirbuteHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");
        var options = new TextOptions() { MatchCase = false, MatchWholeWord = false };

        // Act
        instrumentedHtml.GetByTitle(text, options);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(instrumentedHtml.GetByTitle))))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("text")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(text)))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey(nameof(TextOptions))),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(options.ToString())))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.IsAny<IPropertyBagValue>()), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void GetByTitleBuildsTestSessionWhenProvidedTextOptionsIsNull()
    {
        // Arrange
        var text = "Issues count";
        var instrumentedHtml = new InstrumentedHtmlContent(
            data: HtmlContentTestsHelpers.GetTitleAttirbuteHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");

        // Act
        instrumentedHtml.GetByTitle(text, options: null);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey(nameof(TextOptions))),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>("Null")))), Times.Once);
    }

    [Test]
    public void GetByTitleBuildsTestSessionWithSelectorResults()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            data: HtmlContentTestsHelpers.GetTitleAttirbuteHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<span title=\"Issues count\">25 issues</span>"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByTitle(text: "Issues count");

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string[]>(
                        htmlNodeCollection.Select(n => n.OriginalName.Trim()).ToArray())))), Times.Once);
    }

    [Test]
    public void GetByTitleShouldCreateSuccessfulTestStepWhenCompletedSuccessfully()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            data: HtmlContentTestsHelpers.GetTitleAttirbuteHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<span title='Issues count'>25 issues</span>"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByTitle(text: "Issues count");

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder).Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void GetByTitleInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            data: HtmlContentTestsHelpers.GetTitleAttirbuteHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<span title='Issues count'>25 issues</span>"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.GetByTitle(text: "Issues count");

        // Assert
        Mock.Get(instrumentedHtml.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }

    [Test]
    public void GetByLocatorBuildsTestSession()
    {
        // Arrange
        var selector = XPathExpression.Compile("//button");
        var instrumentedHtml = new InstrumentedHtmlContent(
            data: HtmlContentTestsHelpers.GetLocatorHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");
        var options = new FilterOptions() { HasText = "Itinerary" };

        // Act
        instrumentedHtml.Locator(selector, options);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("MethodName")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(nameof(instrumentedHtml.Locator))))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("selector")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(selector.ToString()!)))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey(nameof(FilterOptions))),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>(options.ToString())))), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.IsAny<IPropertyBagValue>()), Times.Once);
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void GetByLocatorBuildsTestSessionWhenProvidedFilterOptionsIsNull()
    {
        // Arrange
        var selector = XPathExpression.Compile("//button");
        var instrumentedHtml = new InstrumentedHtmlContent(
            data: HtmlContentTestsHelpers.GetLocatorHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");

        // Act
        instrumentedHtml.Locator(selector, options: null);

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey(nameof(FilterOptions))),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string>("Null")))), Times.Once);
    }

    [Test]
    public void GetByLocatorBuildsTestSessionWithSelectorResults()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            data: HtmlContentTestsHelpers.GetLocatorHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<button data-pw=\"directions\">Itinerary</button>"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.Locator(selector: XPathExpression.Compile("//button"));

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder)
            .Verify(m => m.Build(
                It.Is<PropertyBagKey>(p => p == new PropertyBagKey("Nodes")),
                It.Is<IPropertyBagValue>(p =>
                    p.Equals(new PropertyBagValue<string[]>(
                        htmlNodeCollection.Select(n => n.OriginalName.Trim()).ToArray())))), Times.Once);
    }

    [Test]
    public void GetByLocatorShouldCreateSuccessfulTestStepWhenCompletedSuccessfully()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            data: HtmlContentTestsHelpers.GetLocatorHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<button data-pw=\"directions\">Itinerary</button>"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.Locator(selector: XPathExpression.Compile("//button"));

        // Assert
        Mock.Get(instrumentedHtml.Context.SessionBuilder).Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public void GetByLocatorInvokesReportOnProgressContextWhenProgressInstanceAvailable()
    {
        // Arrange
        var instrumentedHtml = new InstrumentedHtmlContentUnderTest(
            data: HtmlContentTestsHelpers.GetLocatorHtml(),
            context: HtmlContentTestsHelpers.CreateTestContext(),
            testIdAttribute: "data-testid");
        var htmlNodeCollection = new HtmlNodeCollection(
            parentnode: new HtmlNode(HtmlNodeType.Element, instrumentedHtml.Document, 0))
        {
            HtmlNode.CreateNode($"<button data-pw=\"directions\">Itinerary</button>"),
        };

        instrumentedHtml.MockedSelector
            .Setup(m => m.Select(It.IsAny<HtmlNode>()))
            .Returns(htmlNodeCollection);

        // Act
        instrumentedHtml.Locator(selector: XPathExpression.Compile("//button"));

        // Assert
        Mock.Get(instrumentedHtml.Context.Progress!).Verify(m => m.Report(It.IsAny<TestStep>()), Times.Once);
    }
}