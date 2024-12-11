using HtmlAgilityPack;
using Moq;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Session;
using TestContext = Xping.Sdk.Core.Components.TestContext;

namespace Xping.Sdk.UnitTests.Helpers;

internal static class HtmlContentTestsHelpers
{
    public static TestStep CreateTestStep(PropertyBagKey key, IPropertyBagValue value) =>
        CreateTestStep(new Dictionary<PropertyBagKey, IPropertyBagValue>()
        {
            { key, value }
        });

    public static TestStep CreateTestStep(IDictionary<PropertyBagKey, IPropertyBagValue> properties)
    {
        var testStep = new TestStep()
        {
            Duration = TimeSpan.FromSeconds(1),
            Name = "name",
            PropertyBag = new PropertyBag<IPropertyBagValue>(properties),
            Result = TestStepResult.Failed,
            StartDate = DateTime.UtcNow,
            TestComponentIteration = 1,
            Type = TestStepType.ValidateStep
        };

        return testStep;
    }

    public static TestContext CreateTestContext(
        ITestSessionBuilder? sessionBuilder = null,
        IInstrumentation? instrumentation = null,
        IProgress<TestStep>? progress = null)
    {
        var context = new TestContext(
            sessionBuilder ?? Mock.Of<ITestSessionBuilder>(),
            instrumentation ?? Mock.Of<IInstrumentation>(),
            progress ?? Mock.Of<IProgress<TestStep>>());

        ConfigureSessionBuilderToReturnSelf(context.SessionBuilder);

        return context;
    }

    public static HtmlNodeCollection CreateHtmlNodeCollection(int count)
    {
        var nodes = new HtmlNodeCollection(parentnode: HtmlNode.CreateNode(GetLabelHtml()));

        for (int i = 0; i < count; i++)
        {
            nodes.Add(HtmlNode.CreateNode("<label>Password <input type=\"password\" /></label>"));
        };

        return nodes;
    }

    public static string GetTitleHtml(string? title = null)
    {
        return
            $"<html>" +
            $"  <head>" +
            $"    <title>{title ?? "Title"}</title>" +
            $"  </head>" +
            $"</html>";
    }

    public static string GetAltHtml(string? alt = null)
    {
        return
            $"<html>" +
            $"  <head>" +
            $"    <title>Title</title>" +
            $"  </head>" +
            $"  <body>" +
            $"    <img alt=\"{alt ?? "logo"}\" src=\"/img/xping365-logo.svg\" width=\"100\" />" +
            $"  </body>" +
            $"</html>";
    }

    public static string GetLabelHtml()
    {
        return
            $"<html>" +
            $"  <head>" +
            $"    <title>Title</title>" +
            $"  </head>" +
            $"  <body>" +
            $"    <label>Password <input type=\"password\" /></label>" +
            $"  </body>" +
            $"</html>";
    }

    public static string GetPlaceholderHtml()
    {
        return
            $"<html>" +
            $"  <head>" +
            $"    <title>Title</title>" +
            $"  </head>" +
            $"  <body>" +
            $"    <input type=\"email\" placeholder=\"name@example.com\" />" +
            $"  </body>" +
            $"</html>";
    }

    public static string GetTestIdHtml()
    {
        return
            $"<html>" +
            $"  <head>" +
            $"    <title>Title</title>" +
            $"  </head>" +
            $"  <body>" +
            $"    <button data-testid=\"directions\">Itinéraire</button>" +
            $"  </body>" +
            $"</html>";
    }

    public static string GetTitleAttirbuteHtml()
    {
        return
            $"<html>" +
            $"  <head>" +
            $"    <title>Title</title>" +
            $"  </head>" +
            $"  <body>" +
            $"    <span title=\"Issues count\">25 issues</span>" +
            $"  </body>" +
            $"</html>";
    }

    public static string GetLocatorHtml()
    {
        return
            $"<html>" +
            $"  <head>" +
            $"    <title>Title</title>" +
            $"  </head>" +
            $"  <body>" +
            $"    <button data-pw=\"directions\">Itinéraire</button>" +
            $"  </body>" +
            $"</html>";
    }

    /// <summary>
    /// Configures a mocked <see cref="ITestSessionBuilder"/> instance to return itself when the <c>Build</c> or 
    /// <c>Initiate</c> method is called. This validates the builder pattern where methods are invoked on the same 
    /// builder instance.
    /// </summary>
    /// <param name="sessionBuilder">The instrumented HTML context containing the session builder.</param>
    /// <remarks>
    /// When the <c>Build</c> method is invoked with any parameters, it returns the same 
    /// <see cref="ITestSessionBuilder"/> instance.
    /// This ensures that subsequent method calls can be chained on the same builder.
    /// </remarks>
    private static void ConfigureSessionBuilderToReturnSelf(ITestSessionBuilder sessionBuilder)
    {
        Mock.Get(sessionBuilder)
            .Setup(_m => _m.Build(
                It.IsAny<PropertyBagKey>(),
                It.IsAny<IPropertyBagValue>()))
            .Returns(sessionBuilder);

        Mock.Get(sessionBuilder)
            .Setup(_m => _m.Initiate(
                It.IsAny<Uri>(),
                It.IsAny<DateTime>(),
                It.IsAny<TestContext>()))
            .Returns(sessionBuilder);
    }
}