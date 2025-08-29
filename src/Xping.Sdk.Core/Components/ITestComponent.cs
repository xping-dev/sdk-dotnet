/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Session;

namespace Xping.Sdk.Core.Components;

/// <summary>
/// The ITestComponent interface defines the methods and properties for a TestComponent base class.
/// </summary>
/// <remarks>
/// This interface is implemented by the TestComponent base class and is intended for unit testing purposes.
/// </remarks>
public interface ITestComponent
{
    /// <summary>
    /// Gets a step name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a step type.
    /// </summary>
    TestStepType Type { get; }

    /// <summary>
    /// Gets a brief description of what the test component does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets a user-friendly display name for the test component.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// This method performs the test step operation asynchronously.
    /// </summary>
    /// <param name="url">A Uri object that represents the URL of the page being validated.</param>
    /// <param name="settings">A <see cref="TestSettings"/> object that contains the settings for the test.</param>
    /// <param name="context">A <see cref="TestContext"/> object that represents the test context.</param>
    /// <param name="serviceProvider">An instance object of a mechanism for retrieving a service object.</param>
    /// <param name="cancellationToken">An optional CancellationToken object that can be used to cancel the 
    /// this operation.</param>
    Task HandleAsync(
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
    /// <param name="cancellationToken">An optional CancellationToken object that can be used to cancel the 
    /// this operation.</param>
    /// <returns><c>true</c> if the test succeeded; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This method is only meant for quick test validation and is stateless.
    /// </remarks>
    Task<bool> ProbeAsync(
        Uri url,
        TestSettings settings,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new instance of the TestComponent class to the current object.
    /// </summary>
    /// <param name="component">The TestComponent instance to add.</param>
    void AddComponent(ITestComponent component);

    /// <summary>
    /// Removes the specified instance of the TestComponent class from the current object.
    /// </summary>
    /// <param name="component">The TestComponent instance to remove.</param>
    /// <returns>
    /// <c>true</c> if component is successfully removed; otherwise, <c>false</c>. This method also returns <c>false</c>
    /// when component was not found.
    /// </returns>
    bool RemoveComponent(ITestComponent component);

    /// <summary>
    /// Gets a read-only collection of the child TestComponent instances of the current object.
    /// </summary>
    IReadOnlyCollection<ITestComponent> Components { get; }
}
