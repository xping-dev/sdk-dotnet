using Xping.Sdk.Core.Components;

namespace Xping.Sdk.Core.Extensions;

/// <summary>
/// Provides extension methods for the TestAgent class to simplify the usage and management of test components.
/// </summary>
public static class TestAgentExtensions
{
    /// <summary>
    /// Adds a new test component to the TestAgent's container.
    /// </summary>
    /// <typeparam name="T">The type of the test component to add, which must implement ITestComponent.</typeparam>
    /// <param name="testAgent">The TestAgent to which the component will be added.</param>
    /// <param name="component">The test component instance to add.</param>
    /// <returns>The TestAgent with the component added to its container.</returns>
    /// <exception cref="ArgumentNullException">Thrown if either the testAgent or the component is null.</exception>
    public static TestAgent Add<T>(this TestAgent testAgent, T component) where T : ITestComponent
    {
        ArgumentNullException.ThrowIfNull(testAgent, nameof(testAgent));
        ArgumentNullException.ThrowIfNull(component, nameof(component));

        testAgent.Container.AddComponent(component);

        return testAgent;
    }

    /// <summary>
    /// Replaces an existing test component of the same name or adds a new one if it does not exist in the TestAgent's
    /// container.
    /// </summary>
    /// <typeparam name="T">The type of the test component to reuse, which must implement ITestComponent.</typeparam>
    /// <param name="testAgent">The TestAgent whose component will be replaced or added.</param>
    /// <param name="component">The test component instance to reuse.</param>
    /// <returns>The TestAgent with the component reused in its container.</returns>
    /// <exception cref="ArgumentNullException">Thrown if either the testAgent or the component is null.</exception>
    public static TestAgent Replace<T>(this TestAgent testAgent, T component) where T : ITestComponent
    {
        ArgumentNullException.ThrowIfNull(testAgent, nameof(testAgent));
        ArgumentNullException.ThrowIfNull(component, nameof(component));

        ITestComponent? existingComponent =
            testAgent.Container.Components.FirstOrDefault(c => c.Name == component.Name);

        if (existingComponent != null)
        {
            testAgent.Container.RemoveComponent(existingComponent);
        }

        testAgent.Container.AddComponent(component);

        return testAgent;
    }
}
