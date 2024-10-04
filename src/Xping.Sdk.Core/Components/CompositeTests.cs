using Xping.Sdk.Core.Session;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Components;

interface ICompositeTests
{
    void AddComponent(ITestComponent component);
    bool RemoveComponent(ITestComponent component);
    void Clear();
    IReadOnlyCollection<ITestComponent> Components { get; }
}

/// <summary>
/// Represents an abstract class that is used to execute a composite test component.
/// </summary>
public abstract class CompositeTests : TestComponent, ICompositeTests
{
    private readonly List<ITestComponent> _components = [];

    /// <summary>
    /// Initializes a new instance of the CompositeTests class with the specified name and test step type.
    /// </summary>
    /// <param name="name">The name of the composite test component.</param>
    protected CompositeTests(string name) : base(name, TestStepType.CompositeStep)
    { }

    /// <summary>
    /// Adds a new instance of the TestComponent class to the current object.
    /// </summary>
    /// <param name="component">The TestComponent instance to add.</param>
    public new void AddComponent(ITestComponent component) => 
        _components.Add(component.RequireNotNull(nameof(component)));

    /// <summary>
    /// Removes the specified instance of the TestComponent class from the current object.
    /// </summary>
    /// <param name="component">The TestComponent instance to remove.</param>
    /// <returns>
    /// <c>true</c> if component is successfully removed; otherwise, <c>false</c>. This method also returns <c>false</c>
    /// when component was not found.
    /// </returns>
    public new bool RemoveComponent(ITestComponent component) => _components.Remove(component);

    /// <summary>
    /// Clears all the components from the current instance.
    /// </summary>
    /// <remarks>
    /// This method is called to clean up the test components. It ensures that the components collection is emptied, 
    /// preventing cross-contamination of state between tests.
    /// </remarks>
    public void Clear() => _components.Clear();

    /// <summary>
    /// Gets a read-only collection of the child TestComponent instances of the current object.
    /// </summary>
    public new IReadOnlyCollection<ITestComponent> Components => _components;

    internal override ICompositeTests? GetComposite() => this;
}
