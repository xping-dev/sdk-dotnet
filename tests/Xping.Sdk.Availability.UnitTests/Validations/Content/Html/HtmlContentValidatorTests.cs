/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Text;
using Moq;
using Xping.Sdk.UnitTests.Helpers;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Validations.Content.Html;
using TestContext = Xping.Sdk.Core.Components.TestContext;

namespace Xping.Sdk.UnitTests.Validations.Content.Html;

public sealed class HtmlContentValidatorTests
{
    private Uri _url;
    private TestSettings _settings;
    private TestContext _context;
    private IServiceProvider _serviceProvider;

    private sealed class HtmlContentValidatorUnderTest(Action<IHtmlContent> validation) : 
        HtmlContentValidator(validation)
    {
        public Action<string, TestContext, string>? OnCreateHtmlContent { get; set; }

        protected override IHtmlContent CreateHtmlContent(string content, TestContext context, string testIdAttribute)
        {
            OnCreateHtmlContent?.Invoke(content, context, testIdAttribute);
            return base.CreateHtmlContent(content, context, testIdAttribute);
        }
    }

    [SetUp]
    public void SetUp()
    {
        _url = new("http://localhost");
        _settings = new();
        _context = new(Mock.Of<ITestSessionBuilder>(), Mock.Of<IInstrumentation>());
        _serviceProvider = Mock.Of<IServiceProvider>();
    }

    [Test]
    public void CtorThrowsArgumentNullExceptionWhenValidationFunctionIsNull()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => new HtmlContentValidator(validation: null!));
    }

    [Test]
    public void HandleAsyncThrowsArgumentNullExceptionWhenUrlIsNull()
    {
        // Arrange
        var validator = new HtmlContentValidator((IHtmlContent content) => { });

        // Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            validator.HandleAsync(url: null!, _settings, _context, _serviceProvider);
        });
    }

    [Test]
    public void HandleAsyncThrowsArgumentNullExceptionWhenSettingsIsNull()
    {
        // Arrange
        var validator = new HtmlContentValidator((IHtmlContent content) => { });

        // Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            validator.HandleAsync(_url, settings: null!, _context, _serviceProvider);
        });
    }

    [Test]
    public void HandleAsyncThrowsArgumentNullExceptionWhenContextIsNull()
    {
        // Arrange
        var validator = new HtmlContentValidator((IHtmlContent content) => { });

        // Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            validator.HandleAsync(_url, _settings, context: null!, _serviceProvider);
        });
    }

    [Test]
    public void HandleAsyncThrowsArgumentNullExceptionWhenServiceProviderIsNull()
    {
        // Arrange
        var validator = new HtmlContentValidator((IHtmlContent content) => { });

        // Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            validator.HandleAsync(_url, _settings, _context, serviceProvider: null!);
        });
    }

    [Test]
    public async Task HandleAsyncInsufficientDataWhenHttpResponseAndHttpContentAreMissing()
    {
        // Arrange
        var validator = new HtmlContentValidator((IHtmlContent content) => { });
        var mockSessionBuilder =
            Mock.Get(_context.SessionBuilder)
                .Setup(m => m.Steps)
                .Returns([]); // Returns an empty steps collection

        // Act
        await validator.HandleAsync(_url, _settings, _context, _serviceProvider);

        // Assert
        Mock.Get(_context.SessionBuilder)
            .Verify(m => m.Build(It.Is<Error>(p => p == Errors.InsufficientData(validator))));
    }

    [Test]
    public async Task HandleAsyncInsufficientDataWhenHttpResponseIsMissingAndHttpContentIsPresent()
    {
        // Arrange
        var testStep = HtmlContentTestsHelpers.CreateTestStep(
            PropertyBagKeys.HttpContent,
            new PropertyBagValue<byte[]>(Encoding.UTF8.GetBytes("Data")));

        var validator = new HtmlContentValidator((IHtmlContent content) => { });
        var mockSessionBuilder =
            Mock.Get(_context.SessionBuilder)
                .Setup(m => m.Steps)
                .Returns([testStep]);

        // Act
        await validator.HandleAsync(_url, _settings, _context, _serviceProvider);

        // Assert
        Mock.Get(_context.SessionBuilder)
            .Verify(m => m.Build(It.Is<Error>(p => p == Errors.InsufficientData(validator))));
    }

    [Test]
    public async Task HandleAsyncInsufficientDataWhenHttpResponseIsPresentAndHttpContentIsMissing()
    {
        // Arrange
        using var httpResponseMessage = new HttpResponseMessage();

        var testStep = HtmlContentTestsHelpers.CreateTestStep(
            PropertyBagKeys.HttpResponseMessage,
            new NonSerializable<HttpResponseMessage>(httpResponseMessage));

        var validator = new HtmlContentValidator((IHtmlContent content) => { });
        var mockSessionBuilder =
            Mock.Get(_context.SessionBuilder)
                .Setup(m => m.Steps)
                .Returns([testStep]);

        // Act
        await validator.HandleAsync(_url, _settings, _context, _serviceProvider);

        // Assert
        Mock.Get(_context.SessionBuilder)
            .Verify(m => m.Build(It.Is<Error>(p => p == Errors.InsufficientData(validator))));
    }

    [Test]
    public async Task HandleAsyncCallsValidationWhenHttpResponseMessageAndHttpContentArePresent()
    {
        // Arrange
        using var httpResponseMessage = new HttpResponseMessage();

        var testStep = HtmlContentTestsHelpers.CreateTestStep(
            new Dictionary<PropertyBagKey, IPropertyBagValue>()
            {
                { PropertyBagKeys.HttpResponseMessage, new NonSerializable<HttpResponseMessage>(httpResponseMessage) },
                { PropertyBagKeys.HttpContent, new PropertyBagValue<byte[]>(Encoding.UTF8.GetBytes("Data")) }
            });

        bool validationCalled = false;
        var validator = new HtmlContentValidator((IHtmlContent content) => { validationCalled = true; });
        var mockSessionBuilder =
            Mock.Get(_context.SessionBuilder)
                .Setup(m => m.Steps)
                .Returns([testStep]);

        // Act
        await validator.HandleAsync(_url, _settings, _context, _serviceProvider);

        // Assert
        Assert.That(validationCalled, Is.True);
    }

    [Ignore("It is no longer possible to have an empty session during validation.")]
    [Test]
    public async Task HandleAsyncCallsSessionBuilderBuildWhenNoStepsHaveBeenCreatedDuringValidation()
    {
        // Arrange
        using var httpResponseMessage = new HttpResponseMessage();

        var testStep = HtmlContentTestsHelpers.CreateTestStep(
            new Dictionary<PropertyBagKey, IPropertyBagValue>()
            {
                { PropertyBagKeys.HttpResponseMessage, new NonSerializable<HttpResponseMessage>(httpResponseMessage) },
                { PropertyBagKeys.HttpContent, new PropertyBagValue<byte[]>(Encoding.UTF8.GetBytes("Data")) }
            });

        var validator = new HtmlContentValidator((IHtmlContent content) =>
        {
            // When validation happens reset number of steps to simulate no steps have been created during validation.
            Mock.Get(_context.SessionBuilder)
                .Setup(m => m.Steps)
                .Returns([]); // Simulate no steps after validation
        });

        Mock.Get(_context.SessionBuilder)
            .Setup(m => m.Steps)
            .Returns([testStep]);

        // Act
        await validator.HandleAsync(_url, _settings, _context, _serviceProvider);

        // Assert
        Mock.Get(_context.SessionBuilder)
            .Verify(m => m.Build(), Times.Once);
    }

    [Test]
    public async Task HandleAsyncPassesHttpContentFromPropertyBag()
    {
        // Arrange
        const string expectedData = "Data";
        using var httpResponseMessage = new HttpResponseMessage();

        var testStep = HtmlContentTestsHelpers.CreateTestStep(
            new Dictionary<PropertyBagKey, IPropertyBagValue>()
            {
                { PropertyBagKeys.HttpResponseMessage, new NonSerializable<HttpResponseMessage>(httpResponseMessage) },
                { PropertyBagKeys.HttpContent, new PropertyBagValue<byte[]>(Encoding.UTF8.GetBytes(expectedData)) }
            });
        var receivedContent = string.Empty;
        var validatorUnderTest = new HtmlContentValidatorUnderTest(_ => { })
        {
            OnCreateHtmlContent = (content, _, _) =>
            {
                receivedContent = content;
            }
        };

        Mock.Get(_context.SessionBuilder)
            .Setup(m => m.Steps)
            .Returns([testStep]);

        // Act
        await validatorUnderTest.HandleAsync(_url, _settings, _context, _serviceProvider);

        // Assert
        Assert.That(receivedContent, Is.EqualTo(expectedData));
    }
}
