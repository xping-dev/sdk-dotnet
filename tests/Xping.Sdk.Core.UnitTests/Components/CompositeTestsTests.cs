/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Moq;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;

namespace Xping.Sdk.UnitTests.Components;

internal class CompositeTestsTests
{
    private class CompositeTestsUnderTest(string name = nameof(CompositeTestsUnderTest)) : CompositeTests(name, "Test composite for unit testing")
    {
        public override Task HandleAsync(
            Uri url,
            TestSettings settings,
            Core.Components.TestContext context,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Test]
    public void ReturnsEmptyComponentListNotNullWhenNewlyInstantiated()
    {
        // Arrange
        const int expectedItemsCount = 0;

        // Act
        var composite = new CompositeTestsUnderTest();

        // Assert
        Assert.That(composite.Components, Is.Not.Null);
        Assert.That(composite.Components.Count, Is.EqualTo(expectedItemsCount));
    }

    [Test]
    public void GetCompositeReturnsItSelf()
    {
        // Arrange
        var composite = new CompositeTestsUnderTest();

        // Assert
        Assert.That(composite.GetComposite(), Is.EqualTo(composite));
    }

    [Test]
    public void ComponentsReturnItemWhenAdded()
    {
        // Arrange
        const int expectedItemCount = 1;

        var composite = new CompositeTestsUnderTest();
        var component = Mock.Of<ITestComponent>();
        
        // Act
        composite.AddComponent(component);

        // Assert
        Assert.That(composite.Components.Count, Is.EqualTo(expectedItemCount));
        Assert.That(composite.Components.First, Is.EqualTo(component));
    }

    [Test]
    public void ComponentsDoesNotReturnItemWhenRemoved()
    {
        // Arrange
        const int expectedItemCount = 0;

        var composite = new CompositeTestsUnderTest();
        var component = Mock.Of<ITestComponent>();

        // Act
        composite.AddComponent(component);
        composite.RemoveComponent(component);

        // Assert
        Assert.That(composite.Components.Count, Is.EqualTo(expectedItemCount));
    }

    [Test]
    public void ComponentNotRemovedWhenNotIncludedInTheComponetsList()
    {
        // Arrange
        const int expectedItemCount = 1;

        var composite = new CompositeTestsUnderTest();

        // Act
        composite.AddComponent(Mock.Of<ITestComponent>());
        composite.RemoveComponent(Mock.Of<ITestComponent>()); // This is new component not added into composite

        // Assert
        Assert.That(composite.Components.Count, Is.EqualTo(expectedItemCount));
    }

    [Test]
    public void AddComponentThrowsArgumentNullExceptionWhenNullComponentAdded()
    {
        // Arrange
        var composite = new CompositeTestsUnderTest();

        // Assert
        Assert.Throws<ArgumentNullException>(() => composite.AddComponent(null!));
    }

    [Test]
    public void RemoveComponentDoesNothingWhenNullComponentRemoved()
    {
        // Arrange
        var composite = new CompositeTestsUnderTest();

        // Assert
        Assert.DoesNotThrow(() => composite.RemoveComponent(null!));
    }

    [Test]
    public void TypeReturnsCompositeStep()
    {
        var composite = new CompositeTestsUnderTest();

        Assert.That(composite.Type, Is.EqualTo(TestStepType.CompositeStep));
    }
}
