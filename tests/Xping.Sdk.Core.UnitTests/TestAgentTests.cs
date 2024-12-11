using Moq;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using Xping.Sdk.UnitTests.TestFixtures;

namespace Xping.Sdk.Core.UnitTests;

[SetUpFixture]
[TestFixtureSource(typeof(TestFixtureProvider), nameof(TestFixtureProvider.ServiceProvider))]
public sealed class TestAgentTests(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly Mock<TestComponent> _testComponentMock = new("StepName", TestStepType.ActionStep);

    [SetUp]
    public void Setup()
    {
        _testComponentMock.Reset();
    }

    [Test]
    public void ContainerIsNotNullWhenInstantiated()
    {
        // Arrange
        using var testAgent = new TestAgent(_serviceProvider);

        // Assert
        Assert.That(testAgent.Container, Is.Not.Null);
    }

    [Test]
    public void RunAsyncThrowsArgumentNullExceptionWhenUrlIsNull()
    {
        // Arrange
        using var testAgent = new TestAgent(_serviceProvider);

        // Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () => 
                await testAgent.RunAsync(url: null!, new TestSettings())
            .ConfigureAwait(false));
    }

    [Test]
    public void RunAsyncDoesNotThrowArgumentNullExceptionWhenTestSettingsIsNull()
    {
        // Arrange
        using var testAgent = new TestAgent(_serviceProvider);

        // Assert
        Assert.DoesNotThrow(() => 
            testAgent.RunAsync(
                url: new Uri("http://test"),
                settings: null)
            .ConfigureAwait(false));
    }
}
