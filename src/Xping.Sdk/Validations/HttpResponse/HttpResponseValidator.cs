/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Shared;
using Xping.Sdk.Validations.HttpResponse.Internals;
using Xping.Sdk.Actions;

namespace Xping.Sdk.Validations.HttpResponse;

/// <summary>
/// Represents a validator for HTTP response. This class provides a mechanism to assert the validity of HTTP response 
/// based on user-defined criteria.
/// </summary>
public class HttpResponseValidator : TestComponent
{
    // Private field to hold the validation logic.
    private readonly Action<IHttpResponse> _validation;

    /// <summary>
    /// The name of the test component that uniquely identifies a HttpResponseValidator test operation.
    /// </summary>
    public const string StepName = nameof(HttpResponseValidator);

    /// <summary>
    /// Initializes a new instance of the HttpResponseValidator class with a specified validation action.
    /// </summary>
    /// <param name="validation">
    /// An Action delegate that encapsulates the validation logic for the HTTP response.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when the validation action is null.</exception>
    public HttpResponseValidator(Action<IHttpResponse> validation) : base(StepName, TestStepType.ValidateStep)
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

        using var instrumentation = new InstrumentationTimer();
        TestStep? testStep = null;

        try
        {
            var responseMessage = context.GetNonSerializablePropertyBagValue<HttpResponseMessage>(
                PropertyBagKeys.HttpResponseMessage);
            var responseTime = GetRequestDuration(context);

            if (responseMessage == null || !responseTime.HasValue)
            {
                testStep = context.SessionBuilder.Build(error: Errors.InsufficientData(component: this));
            }
            else
            {
                // Perform HTTP headers validation.
                _validation.Invoke(new HttpResponseInfo(responseMessage, responseTime.Value, context));
            }
        }
        catch (ValidationException ex)
        {
            testStep = context.SessionBuilder.Build(Errors.ValidationFailed(component: this, errorMessage: ex.Message));
        }
        catch (Exception exception)
        {
            testStep = context.SessionBuilder.Build(exception);
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

    private static TimeSpan? GetRequestDuration(TestContext context)
    {
        var steps = context.SessionBuilder.Steps;
        var testStep =
            GetHttpClientRequestSenderStep(steps) ??
            GetBrowserRequestSenderStep(steps);

        var duration = testStep?.Duration;
        return duration;
    }

    private static TestStep? GetHttpClientRequestSenderStep(IReadOnlyCollection<TestStep> steps)
    {
        var testStep = steps.FirstOrDefault(step => step.Name == nameof(HttpClientRequestSender));
        return testStep;
    }

    private static TestStep? GetBrowserRequestSenderStep(IReadOnlyCollection<TestStep> steps)
    {
        var testStep = steps.FirstOrDefault(step => step.Name == nameof(BrowserRequestSender));
        return testStep;
    }
}