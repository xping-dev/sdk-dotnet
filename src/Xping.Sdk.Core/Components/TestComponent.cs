/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Shared;
using Xping.Sdk.Core.Session;
using Microsoft.Extensions.DependencyInjection;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Session.Collector;

namespace Xping.Sdk.Core.Components;

/// <summary>
/// This abstract class provides the base implementation for executing test operations, either as actions or
/// validations, as defined by derived classes.
/// </summary>
public abstract class TestComponent : ITestComponent
{
    private static IReadOnlyCollection<TestComponent> EmptyComponents { get; } = [];
    
    /// <summary>
    /// Initializes a new instance of the TestComponent class.
    /// </summary>
    /// <param name="name">Name of the test component.</param>
    /// <param name="type">Type of the test component.</param>
    protected TestComponent(string name, TestStepType type)
    {
        Name = name.RequireNotNullOrEmpty(nameof(name));
        Type = type;
    }

    /// <summary>
    /// Gets component name.
    /// </summary>
    public string Name { get; protected set; }

    /// <summary>
    /// Gets a test component type.
    /// </summary>
    public TestStepType Type { get; }

    /// <summary>
    /// This method performs the test step operation asynchronously. It is an abstract method that must be implemented 
    /// by the subclass.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the page being validated.</param>
    /// <param name="settings">A <see cref="TestSettings"/> object that contains the settings for the test.</param>
    /// <param name="context">A <see cref="TestContext"/> object that represents the test context.</param>
    /// <param name="serviceProvider">An instance object of a mechanism for retrieving a service object.</param>
    /// <param name="cancellationToken">An optional CancellationToken object that can be used to cancel 
    /// this operation.</param>
    public abstract Task HandleAsync(
        Uri url,
        TestSettings settings,
        TestContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously performs a probe test on the specified URL using the specified settings.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the page being validated.</param>
    /// <param name="settings">A <see cref="TestSettings"/> object that contains the settings for the test.</param>
    /// <param name="serviceProvider">An instance object of a mechanism for retrieving a service object.</param>
    /// <param name="cancellationToken">An optional CancellationToken object that can be used to cancel 
    /// this operation.</param>
    /// <returns><c>true</c> if the test succeeded; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This method is only meant for quick test validation and is stateless.
    /// </remarks>
    public virtual async Task<bool> ProbeAsync(
        Uri url,
        TestSettings settings,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url, nameof(url));
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));

        using var instrumentation = new InstrumentationTimer(startStopwatch: false);
        var sessionBuilder = serviceProvider.GetRequiredService<ITestSessionBuilder>();
        var sessionUploader = serviceProvider.GetRequiredService<ITestSessionUploader>();
        
        // Create a temporary pipeline containing just this component for probe testing
        var pipeline = new Pipeline("ProbeTest", this);

        var context = new TestContext(
            sessionBuilder: sessionBuilder,
            instrumentation: instrumentation,
            sessionUploader: sessionUploader,
            pipeline: pipeline,
            progress: serviceProvider.GetService<IProgress<TestStep>>());

        // Update context with a currently executing component.
        context.UpdateExecutionContext(this);

        // Initiate the test session by recording its start time, the URL of the page being validated,
        // and associating it with the current TestContext responsible for maintaining the state of the test
        // execution. No upload on Probe.
        sessionBuilder.Initiate(url, DateTime.UtcNow, context, uploadToken: Guid.Empty);

        // Execute the test operation by invoking the HandleAsync method of this class.
        await HandleAsync(url, settings, context, serviceProvider, cancellationToken).ConfigureAwait(false);

        return !context.SessionBuilder.HasFailed;
    }

    /// <summary>
    /// Adds a new instance of the TestComponent class to the current object.
    /// </summary>
    /// <param name="component">The TestComponent instance to add.</param>
    public void AddComponent(ITestComponent component) => GetComposite()?.AddComponent(component);

    /// <summary>
    /// Removes the specified instance of the TestComponent class from the current object.
    /// </summary>
    /// <param name="component">The TestComponent instance to remove.</param>
    /// <returns>
    /// <c>true</c> if component is successfully removed; otherwise, <c>false</c>. This method also returns <c>false</c>
    /// when component was not found.
    /// </returns>
    public bool RemoveComponent(ITestComponent component) => GetComposite()?.RemoveComponent(component) ?? false;

    /// <summary>
    /// Gets a read-only test component collection.
    /// </summary>
    public IReadOnlyCollection<ITestComponent> Components => GetComposite()?.Components ?? EmptyComponents;

    internal virtual ICompositeTests? GetComposite() => null;
}
