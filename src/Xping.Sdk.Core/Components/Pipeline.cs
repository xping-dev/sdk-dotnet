/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Components;

/// <summary>
/// The Pipeline class is a concrete implementation of the <see cref="CompositeTests"/> class that is designed to run 
/// and manage the test components that have been added.
/// </summary>
public class Pipeline : CompositeTests
{
    /// <summary>
    /// Initializes a new instance of the Pipeline class with the specified name and components.
    /// </summary>
    /// <param name="name">The optional name of the pipeline. If null, the StepName constant is used.</param>
    /// <param name="components">The test components that make up the pipeline.</param>
    /// <remarks>
    /// The Pipeline class inherits from the CompositeTests class and represents a sequence of tests that can be 
    /// executed as a single unit.
    /// </remarks>
    public Pipeline(
        string? name = null,
        params ITestComponent[] components) : base(name ?? nameof(Pipeline))
    {
        if (components != null)
        {
            foreach (var component in components)
            {
                AddComponent(component);
            }
        }
    }

    /// <summary>
    /// This method is designed to perform the test components that have been included in the current object.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the page being validated.</param>
    /// <param name="settings">A <see cref="TestSettings"/> object that contains the settings for the test.</param>
    /// <param name="context">A <see cref="TestContext"/> object that represents the test context.</param>
    /// <param name="serviceProvider">An instance object of a mechanism for retrieving a service object.</param>
    /// <param name="cancellationToken">An optional CancellationToken object that can be used to cancel this operation.
    /// </param>
    public override async Task HandleAsync(
        Uri url,
        TestSettings settings,
        TestContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var component in Components)
        {
            using var tokenSource = new CancellationTokenSource(settings.Timeout);

            // Update context with currently executing component.
            context.UpdateExecutionContext(component);

            await component
                .HandleAsync(url, settings, context, serviceProvider, tokenSource.Token)
                .ConfigureAwait(false);

            // If the 'ContinueOnFailure' property is set to false and the test context contains a session that has
            // failed, then break the loop.
            if (!settings.ContinueOnFailure && context.SessionBuilder.HasFailed)
            {
                break;
            }
        }
    }
}
