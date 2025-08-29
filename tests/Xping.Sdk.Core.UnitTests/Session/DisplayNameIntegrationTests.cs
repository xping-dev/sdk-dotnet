/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;

namespace Xping.Sdk.UnitTests.Session;

[TestFixture]
internal class DisplayNameIntegrationTests
{
    [Test]
    public void TestStepIncludesDisplayNameFromComponent()
    {
        // Arrange
        var component = new TestComponentMock();
        var builder = CreateSessionBuilder(component);

        // Act
        var testStep = builder.Build();

        // Assert
        Assert.That(testStep.Name, Is.EqualTo(nameof(TestComponentMock)));
        Assert.That(testStep.DisplayName, Is.EqualTo("Test Component Mock"));
        Assert.That(testStep.Description, Is.EqualTo("Mock test component for testing"));
    }

    [Test]
    public void TestStepDisplayNameIsSerializedAndDeserialized()
    {
        // Arrange
        var originalStep = new TestStep
        {
            Name = "TestName",
            Description = "Test Description",
            DisplayName = "Test Display Name",
            TestComponentIteration = 1,
            StartDate = DateTime.UtcNow,
            Duration = TimeSpan.Zero,
            Type = TestStepType.ActionStep,
            Result = TestStepResult.Succeeded,
            PropertyBag = null,
            ErrorMessage = null
        };

        // Act - Serialize and deserialize
        var serializer = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
        using var stream = new MemoryStream();
        
#pragma warning disable SYSLIB0011 // Type or member is obsolete
        serializer.Serialize(stream, originalStep);
        stream.Position = 0;
        var deserializedStep = (TestStep)serializer.Deserialize(stream);
#pragma warning restore SYSLIB0011 // Type or member is obsolete

        // Assert
        Assert.That(deserializedStep.Name, Is.EqualTo(originalStep.Name));
        Assert.That(deserializedStep.DisplayName, Is.EqualTo(originalStep.DisplayName));
        Assert.That(deserializedStep.Description, Is.EqualTo(originalStep.Description));
    }

    private sealed class TestComponentMock : TestComponent
    {
        public TestComponentMock() : base(
            type: TestStepType.ActionStep,
            description: "Mock test component for testing",
            displayName: "Test Component Mock")
        {
        }

        public override Task HandleAsync(Uri url, TestSettings settings, TestContext context, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private static ITestSessionBuilder CreateSessionBuilder(ITestComponent component)
    {
        var builder = new TestSessionBuilder();
        var mockInstrumentation = new MockInstrumentation();
        
        builder.Initiate(
            url: new Uri("https://test.com"),
            startTime: DateTime.UtcNow,
            context: CreateMockContext(builder, mockInstrumentation, component),
            uploadToken: Guid.NewGuid(),
            metadata: new TestMetadata());

        return builder;
    }

    private static TestContext CreateMockContext(ITestSessionBuilder builder, InstrumentationTimer instrumentation, ITestComponent component)
    {
        var pipeline = new Pipeline("Test", component);
        var context = new TestContext(
            sessionBuilder: builder,
            instrumentation: instrumentation,
            sessionUploader: Mock.Of<ITestSessionUploader>(),
            pipeline: pipeline,
            progress: null);

        context.UpdateExecutionContext(component);
        return context;
    }

    private sealed class MockInstrumentation : InstrumentationTimer
    {
        public MockInstrumentation() : base(false) { }
        public override DateTime StartTime => DateTime.UtcNow;
        public override TimeSpan ElapsedTime => TimeSpan.Zero;
    }
}
