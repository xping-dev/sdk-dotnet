namespace Xping.Sdk.UnitTests.Components;

using Moq;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Session;
using TestContext = Core.Components.TestContext;

internal class TestContextTests
{
    [Test]
    public void ConstructorThrowsArgumentNullExecptionWhenISessionBuilderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new TestContext(
            sessionBuilder: null!, 
            instrumentation: Mock.Of<IInstrumentation>(), 
            progress: null));
    }

    [Test]
    public void ConstructorDoesNotThrowWhenISessionBuilderIsNotNull()
    {
        Assert.DoesNotThrow(() => new TestContext(
            sessionBuilder: Mock.Of<ITestSessionBuilder>(), 
            instrumentation: Mock.Of<IInstrumentation>(), 
            progress: null));
    }

    [Test]
    public void ConstructorThrowsArgumentNullExecptionWhenIInstrumentationIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new TestContext(
            sessionBuilder: Mock.Of<ITestSessionBuilder>(),
            instrumentation: null!,
            progress: null));
    }

    [Test]
    public void ConstructorDoesNotThrowWhenIInstrumentationIsNotNull()
    {
        Assert.DoesNotThrow(() => new TestContext(
            sessionBuilder: Mock.Of<ITestSessionBuilder>(),
            instrumentation: Mock.Of<IInstrumentation>(),
            progress: null));
    }

    [Test]
    public void ConstructorDoesNotThrowWhenIProgressIsNull()
    {
        Assert.DoesNotThrow(() => new TestContext(
            sessionBuilder: Mock.Of<ITestSessionBuilder>(), 
            instrumentation: Mock.Of<IInstrumentation>(), 
            progress: null));
    }

    [Test]
    public void ConstructorDoesNotThrowWhenIProgressIsNotNull()
    {
        Assert.DoesNotThrow(() => new TestContext(
            sessionBuilder: Mock.Of<ITestSessionBuilder>(), 
            instrumentation: Mock.Of<IInstrumentation>(), 
            progress: Mock.Of<IProgress<TestStep>>()));
    }
}
