/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Session.Collector;

namespace Xping.Sdk.UnitTests.Components;

using Moq;
using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;
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
            sessionUploader: Mock.Of<ITestSessionUploader>(),
            pipeline: new Pipeline("TestPipeline"),
            progress: null));
    }

    [Test]
    public void ConstructorDoesNotThrowWhenISessionBuilderIsNotNull()
    {
        Assert.DoesNotThrow(() => new TestContext(
            sessionBuilder: Mock.Of<ITestSessionBuilder>(), 
            instrumentation: Mock.Of<IInstrumentation>(), 
            sessionUploader: Mock.Of<ITestSessionUploader>(),
            pipeline: new Pipeline("TestPipeline"),
            progress: null));
    }

    [Test]
    public void ConstructorThrowsArgumentNullExecptionWhenIInstrumentationIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new TestContext(
            sessionBuilder: Mock.Of<ITestSessionBuilder>(),
            instrumentation: null!,
            sessionUploader: Mock.Of<ITestSessionUploader>(),
            pipeline: new Pipeline("TestPipeline"),
            progress: null));
    }

    [Test]
    public void ConstructorDoesNotThrowWhenIInstrumentationIsNotNull()
    {
        Assert.DoesNotThrow(() => new TestContext(
            sessionBuilder: Mock.Of<ITestSessionBuilder>(),
            instrumentation: Mock.Of<IInstrumentation>(),
            sessionUploader: Mock.Of<ITestSessionUploader>(),
            pipeline: new Pipeline("TestPipeline"),
            progress: null));
    }

    [Test]
    public void ConstructorDoesNotThrowWhenIProgressIsNull()
    {
        Assert.DoesNotThrow(() => new TestContext(
            sessionBuilder: Mock.Of<ITestSessionBuilder>(), 
            instrumentation: Mock.Of<IInstrumentation>(), 
            sessionUploader: Mock.Of<ITestSessionUploader>(),
            pipeline: new Pipeline("TestPipeline"),
            progress: null));
    }

    [Test]
    public void ConstructorDoesNotThrowWhenIProgressIsNotNull()
    {
        Assert.DoesNotThrow(() => new TestContext(
            sessionBuilder: Mock.Of<ITestSessionBuilder>(), 
            instrumentation: Mock.Of<IInstrumentation>(), 
            sessionUploader: Mock.Of<ITestSessionUploader>(),
            pipeline: new Pipeline("TestPipeline"),
            progress: Mock.Of<IProgress<TestStep>>()));
    }
}
