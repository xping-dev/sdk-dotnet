using System.ComponentModel.DataAnnotations;
using Moq;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using Xping.Sdk.UnitTests.TestFixtures;
using TestContext = Xping.Sdk.Core.Components.TestContext;

namespace Xping.Sdk.UnitTests.Components;

[SetUpFixture]
[TestFixtureSource(typeof(TestFixtureProvider), nameof(TestFixtureProvider.ServiceProvider))]
internal class PipelineTests(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    [Test]
    public void NameReturnsPipelineNameWhenGivenDuringCreation()
    {
        // Arrange
        const string PipelineName = "MyCustomName";

        // Act
        var pipeline = new Pipeline(name: PipelineName);

        // Assert
        Assert.That(pipeline.Name, Is.EqualTo(PipelineName));
    }

    [Test]
    public void NameReturnsPipelineNameWhenNameHasNotBeenProvided()
    {
        // Arrange
        const string PipelineName = nameof(Pipeline);

        // Act
        var pipeline = new Pipeline();

        // Assert
        Assert.That(pipeline.Name, Is.EqualTo(PipelineName));
    }

    [Test]
    public void ComponentsHaveItemsFromContstructorWhenProvided()
    {
        // Arrange
        const int expectedItemCount = 2;

        var component1 = Mock.Of<ITestComponent>();
        var component2 = Mock.Of<ITestComponent>();
        
        // Act
        var pipeline = new Pipeline(components: [component1, component2]);

        // Assert
        Assert.That(pipeline.Components.Count, Is.EqualTo(expectedItemCount));
        Assert.That(pipeline.Components.First(), Is.EqualTo(component1));
        Assert.That(pipeline.Components.Last(), Is.EqualTo(component2));
    }

    [Test]
    public void ConstructorThrowsArgumentNullExceptionWhenAtLeastOneComponentIsNullFromContstructor()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => new Pipeline(components: [
            Mock.Of<ITestComponent>(), null!, Mock.Of<ITestComponent>()]));
    }

    [Test]
    public async Task HandleAsyncCallsAllComponentsHandleAsyncMethodWhenInvokded()
    {
        // Act
        var components = new ITestComponent[] { Mock.Of<ITestComponent>(), Mock.Of<ITestComponent>() };

        var url = new Uri("http://test");
        var pipeline = new Pipeline(components: components);
        var context = new TestContext(Mock.Of<ITestSessionBuilder>(), Mock.Of<IInstrumentation>(), progress: null);
        var settings = new TestSettings();

        // Arrange
        await pipeline.HandleAsync(url, settings, context, _serviceProvider).ConfigureAwait(false);

        // Assert
        foreach (var component in components)
        {
            Mock.Get(component).Verify(c => c.HandleAsync(
                It.Is<Uri>(u => u == url),
                It.Is<TestSettings>(s => s == settings),
                It.Is<TestContext>(c => c == context),
                It.Is<IServiceProvider>(p => p == _serviceProvider),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Test]
    public async Task HandleAsyncExitsWhenComponentFailsAndTestSettingsHasContinueOnFailureDisabled()
    {
        // Act
        const bool hasFailedResult = true;

        var url = new Uri("http://test");
        var sessionBuilderMock = new Mock<ITestSessionBuilder>();
        sessionBuilderMock.SetupGet(b => b.HasFailed).Returns(hasFailedResult);

        var context = new TestContext(sessionBuilderMock.Object, Mock.Of<IInstrumentation>(), progress: null);
        var settings = new TestSettings
        {
            ContinueOnFailure = false
        };

        var components = new ITestComponent[] { Mock.Of<ITestComponent>(), Mock.Of<ITestComponent>() };
        var pipeline = new Pipeline(components: components);

        // Arrange
        await pipeline.HandleAsync(url, settings, context, _serviceProvider).ConfigureAwait(false);

        // Assert
        Mock.Get(components.First()).Verify(c => c.HandleAsync(
                It.Is<Uri>(u => u == url),
                It.Is<TestSettings>(s => s == settings),
                It.Is<TestContext>(c => c == context),
                It.Is<IServiceProvider>(p => p == _serviceProvider),
                It.IsAny<CancellationToken>()), Times.Once);

        Mock.Get(components.Last()).Verify(c => c.HandleAsync(
                It.Is<Uri>(u => u == url),
                It.Is<TestSettings>(s => s == settings),
                It.Is<TestContext>(c => c == context),
                It.Is<IServiceProvider>(p => p == _serviceProvider),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsyncDoesNotExitWhenComponentFailsAndTestSettingsHasContinueOnFailureEnabled()
    {
        // Act
        const bool hasFailedResult = true;

        var url = new Uri("http://test");
        var sessionBuilderMock = new Mock<ITestSessionBuilder>();
        sessionBuilderMock.SetupGet(b => b.HasFailed).Returns(hasFailedResult);

        var context = new TestContext(sessionBuilderMock.Object, Mock.Of<IInstrumentation>(), progress: null);
        var settings = new TestSettings
        {
            ContinueOnFailure = true
        };

        var components = new ITestComponent[] { Mock.Of<ITestComponent>(), Mock.Of<ITestComponent>() };
        var pipeline = new Pipeline(components: components);

        // Arrange
        await pipeline.HandleAsync(url, settings, context, _serviceProvider).ConfigureAwait(false);

        // Assert
        Mock.Get(components.First()).Verify(c => c.HandleAsync(
                It.Is<Uri>(u => u == url),
                It.Is<TestSettings>(s => s == settings),
                It.Is<TestContext>(c => c == context),
                It.Is<IServiceProvider>(p => p == _serviceProvider),
                It.IsAny<CancellationToken>()), Times.Once);

        Mock.Get(components.Last()).Verify(c => c.HandleAsync(
                It.Is<Uri>(u => u == url),
                It.Is<TestSettings>(s => s == settings),
                It.Is<TestContext>(c => c == context),
                It.Is<IServiceProvider>(p => p == _serviceProvider),
                It.IsAny<CancellationToken>()), Times.Once);
    }
}
