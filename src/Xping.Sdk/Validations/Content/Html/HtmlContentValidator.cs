/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Actions;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Shared;
using Xping.Sdk.Validations.Content.Html.Internals;

namespace Xping.Sdk.Validations.Content.Html;

/// <summary>
/// Represents a validator that checks if a html content matches a specified pattern or condition.
/// </summary>
/// <remarks>
/// <note>
/// The HtmlContentValidator component requires either the Browser <see cref="BrowserRequestSender"/> or HttpClient 
/// <see cref="HttpClientRequestSender"/> component to be registered before it in the pipeline, because it depends on 
/// the HTTP response results from these components.
/// </note>
/// </remarks>
public class HtmlContentValidator : BaseContentValidator
{
    private readonly Action<IHtmlContent> _validation;

    /// <summary>
    /// The name of the test component that represents a StringContentValidator test operation.
    /// </summary>
    /// <remarks>
    /// This constant is used to register the StringContentValidator class in the test framework.
    /// </remarks>
    public const string StepName = nameof(HtmlContentValidator);

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlContentValidator"/> class.
    /// </summary>
    /// <param name="validation">
    /// A function that determines whether the html content matches a specified condition.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when the validation function is null.</exception>
    public HtmlContentValidator(Action<IHtmlContent> validation) : base(StepName)
    {
        _validation = validation.RequireNotNull(nameof(validation));
    }

    /// <summary>
    /// This method performs the test step operation asynchronously.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the page being validated.</param>
    /// <param name="settings">A <see cref="TestSettings"/> object that contains the settings for the test.</param>
    /// <param name="context">A <see cref="TestContext"/> object that represents the test context.</param>
    /// <param name="serviceProvider">An instance object of a mechanism for retrieving a service object.</param>
    /// <param name="cancellationToken">
    /// An optional CancellationToken object that can be used to cancel this operation.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// If any of the following parameters: url, settings or context is null.
    /// </exception>
    public override Task HandleAsync(
        Uri url,
        TestSettings settings,
        TestContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url, nameof(url));
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));
        ArgumentNullException.ThrowIfNull(context, nameof(context));
        ArgumentNullException.ThrowIfNull(serviceProvider, nameof(serviceProvider));

        TestStep? testStep = null;

        try
        {
            var response = GetHttpResponseMessage(context);
            var data = GetData(context);

            if (response == null || data.Length == 0)
            {
                testStep = context.SessionBuilder.Build(error: Errors.InsufficientData(component: this));
            }
            else
            {
                var content = GetContent(data, response.Content.Headers);

                // Perform HTML validation.
                var htmlContent = CreateHtmlContent(content, context, settings.TestIdAttribute); 
                _validation(htmlContent);
            }
        }
        catch (ValidationException ex)
        {
            testStep = context.SessionBuilder.Build(
                Errors.ValidationFailed(component: this, errorMessage: ex.Message));
        }
        catch (Exception ex)
        {
            testStep = context.SessionBuilder.Build(ex);
        }
        finally
        {
            if (testStep != null)
            {
                context.Progress?.Report(testStep);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates an instance of <see cref="IHtmlContent"/> by combining the provided HTML data, test context, 
    /// and a specified test ID attribute.
    /// </summary>
    /// <param name="content">The HTML data to include in the content.</param>
    /// <param name="context">The test context.</param>
    /// <param name="testIdAttribute">The attribute to use for identifying test-specific elements.</param>
    /// <returns>An instance of <see cref="IHtmlContent"/> representing the instrumented HTML content.</returns>
    protected virtual IHtmlContent CreateHtmlContent(string content, TestContext context, string testIdAttribute)
    {
        return new InstrumentedHtmlContent(content, context, testIdAttribute);
    }
}

