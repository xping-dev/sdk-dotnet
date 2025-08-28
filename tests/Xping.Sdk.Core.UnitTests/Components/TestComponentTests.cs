/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Core.Session.Collector;
using Xping.Sdk.UnitTests.TestFixtures;
using TestContext = Xping.Sdk.Core.Components.TestContext;

namespace Xping.Sdk.UnitTests.Components;

[SetUpFixture]
[TestFixtureSource(typeof(TestFixtureProvider), nameof(TestFixtureProvider.ServiceProvider))]
public sealed class TestComponentTests(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    private sealed class TestComponentUnderTest(
        string name = nameof(TestComponentUnderTest),
        TestStepType type = TestStepType.ActionStep,
        Mock<ICompositeTests>? compositeMock = null) : TestComponent(name, type, "Test component for unit testing")
    {
        public bool HandleAsyncCalled { get; private set; }

        public override Task HandleAsync(
            Uri uri,
            TestSettings settings,
            TestContext context,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken = default)
        {
            HandleAsyncCalled = true;
            return Task.CompletedTask;
        }

        public Mock<ICompositeTests>? CompositeMock { get; } = compositeMock;

        internal override ICompositeTests? GetComposite() => CompositeMock?.Object;
    }

    [Test]
    public void ThrowsArgumentExceptionWhenNameIsNullOrEmpty()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => new TestComponentUnderTest(name: null!));
        Assert.Throws<ArgumentException>(() => new TestComponentUnderTest(name: string.Empty));
    }

    [Test]
    public void ProbeThrowsArgumentNullExceptionWhenUrlIsNull()
    {
        // Arrange
        TestComponent component = new TestComponentUnderTest();

        // Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => component.ProbeAsync(
            url: null!, new TestSettings(), _serviceProvider));
    }

    [Test]
    public void ProbeThrowsArgumentNullExceptionWhenTestSettingsIsNull()
    {
        // Arrange
        TestComponent component = new TestComponentUnderTest();

        // Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => component.ProbeAsync(
            url: new Uri("http://test.com"), settings: null!, _serviceProvider));
    }

    [Test]
    public async Task ProbeReturnsTrueWhenTestComponentPasses()
    {
        // Arrange
        const bool testComponentPasses = true;

        var url = new Uri("http://test");
        var settings = new TestSettings();

        var sessionBuilder = new Mock<ITestSessionBuilder>();
        sessionBuilder.SetupGet(b => b.HasFailed).Returns(!testComponentPasses);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(s => s.GetService(typeof(ITestSessionBuilder)))
            .Returns(sessionBuilder.Object);
        serviceProvider
            .Setup(s => s.GetService(typeof(ITestSessionUploader)))
            .Returns(Mock.Of<ITestSessionUploader>());

        TestComponent component = new TestComponentUnderTest();

        // Act
        bool result = await component.ProbeAsync(url, settings, serviceProvider.Object).ConfigureAwait(false);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task ProbeReturnsFalseWhenTestComponentFails()
    {
        // Arrange
        const bool testComponentPasses = false;

        var url = new Uri("http://test");
        var settings = new TestSettings();

        var sessionBuilder = new Mock<ITestSessionBuilder>();
        sessionBuilder.SetupGet(b => b.HasFailed).Returns(!testComponentPasses);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(s => s.GetService(typeof(ITestSessionBuilder)))
            .Returns(sessionBuilder.Object);
        serviceProvider
            .Setup(s => s.GetService(typeof(ITestSessionUploader)))
            .Returns(Mock.Of<ITestSessionUploader>());

        TestComponent component = new TestComponentUnderTest();

        // Act
        bool result = await component.ProbeAsync(url, settings, serviceProvider.Object).ConfigureAwait(false);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ProbeCallsHandleAsyncWhenInvoked()
    {
        // Arrange
        var url = new Uri("http://test");
        var settings = new TestSettings();
        var component = new TestComponentUnderTest();

        // Act
        await component.ProbeAsync(url, settings, _serviceProvider, CancellationToken.None).ConfigureAwait(false);

        Assert.That(component.HandleAsyncCalled, Is.True);
    }

    [Test]
    public void AddComponentIsInvokedWhenGetCompositeReturnsObject()
    {
        // Arrange
        var compositeMock = new Mock<ICompositeTests>();
        var component = new TestComponentUnderTest(compositeMock: compositeMock);

        // Act
        component.AddComponent(Mock.Of<ITestComponent>());

        // Assert
        compositeMock.Verify(mock => mock.AddComponent(It.IsAny<ITestComponent>()), Times.Once);
    }

    [Test]
    public void RemoveComponentIsInvokedWhenGetCompositeReturnsObject()
    {
        // Arrange
        var compositeMock = new Mock<ICompositeTests>();
        var component = new TestComponentUnderTest(compositeMock: compositeMock);

        // Act
        component.RemoveComponent(Mock.Of<ITestComponent>());

        // Assert
        compositeMock.Verify(mock => mock.RemoveComponent(It.IsAny<ITestComponent>()), Times.Once);
    }

    [Test]
    public void ComponentsReturnAnEmptyArrayWhenGetCompositeIsNull()
    {
        // Arrange
        var component = new TestComponentUnderTest(compositeMock: null);

        // Assert
        Assert.That(component.Components, Is.Empty);
    }

    [Test]
    public void ComponentsReturnSpecificArrayWhenGetCompositeIsImplemented()
    {
        // Arrange
        const int expectedChildComponents = 1;
        var compositeMock = new Mock<ICompositeTests>();
        compositeMock.SetupGet(mock => mock.Components).Returns(new[] { Mock.Of<ITestComponent>() });

        // Act
        var component = new TestComponentUnderTest(compositeMock: compositeMock);

        // Assert
        Assert.That(component.Components, Has.Count.EqualTo(expectedChildComponents));
    }
}
