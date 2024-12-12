using Microsoft.Playwright;
using Xping.Sdk.Core.Clients.Browser;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Shared;
using Xping.Sdk.Validations.Content.Page.Internals;

namespace Xping.Sdk.Validations.Content.Page;

/// <summary>
/// Represents a validator that checks if a http content response rendered in the browser matches a specified 
/// pattern or condition.
/// </summary>
/// <remarks>
/// <note>
/// The PageContentValidator component requires the Browser component to be registered before it in the pipeline, 
/// because it depends on the HTTP response results from this component.
/// </note>
/// </remarks>
public class PageContentValidator : BaseContentValidator
{
    private readonly Func<IPage, Task> _validation;

    /// <summary>
    /// The name of the test component that represents a StringContentValidator test operation.
    /// </summary>
    /// <remarks>
    /// This constant is used to register the StringContentValidator class in the test framework.
    /// </remarks>
    public const string StepName = nameof(PageContentValidator);

    /// <summary>
    /// Initializes a new instance of the <see cref="PageContentValidator"/> class.
    /// </summary>
    /// <param name="validation">
    /// A function that determines whether the html content matches a specififed condition.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when the validation function is null.</exception>
    public PageContentValidator(Func<IPage, Task> validation) : base(StepName)
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
    public override async Task HandleAsync(
        Uri url,
        TestSettings settings,
        TestContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url, nameof(url));
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        TestStep? testStep = null;

        try
        {
            var response = GetBrowserResponseMessage(context);

            if (response == null)
            {
                testStep = context.SessionBuilder.Build(error: Errors.InsufficientData(component: this));
            }
            else
            {
                var page = response.Page;

                // Perform Page validation.
                await _validation(new InstrumentedPage(context, page)).ConfigureAwait(false);
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
    }
}

