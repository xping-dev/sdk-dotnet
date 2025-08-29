/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;

namespace Xping.Sdk.Core.UnitTests.Session;

[TestFixture]
internal class DisplayNameIntegrationTests
{
        [Test]
    public void TestStep_CreatedFromComponent_HasCorrectDisplayName()
    {
        // Arrange
        var component = new TestComponentMock();

        // Act
        var testStep = new TestStep()
        {
            Name = component.Name,
            Description = component.Description,
            DisplayName = component.DisplayName,
            TestComponentIteration = 1,
            StartDate = DateTime.UtcNow,
            Duration = TimeSpan.Zero,
            Type = component.Type,
            Result = TestStepResult.Succeeded,
            PropertyBag = null,
            ErrorMessage = null
        };

        // Assert
        Assert.That(testStep.Name, Is.EqualTo(nameof(TestComponentMock)));
        Assert.That(testStep.DisplayName, Is.EqualTo("Test Component Mock"));
        Assert.That(testStep.Description, Is.EqualTo("Mock test component for testing"));
    }

    [Test]
    public void TestStepDisplayNameIsSerializedAndDeserialized()
    {
        // Arrange
        var component = new TestComponentMock();
        var originalStep = new TestStep()
        {
            Name = component.Name,
            Description = component.Description,
            DisplayName = component.DisplayName,
            TestComponentIteration = 1,
            StartDate = DateTime.UtcNow,
            Duration = TimeSpan.Zero,
            Type = component.Type,
            Result = TestStepResult.Succeeded,
            PropertyBag = null,
            ErrorMessage = null
        };

        // Act - Serialize and deserialize using System.Text.Json (secure alternative)
        var json = JsonSerializer.Serialize(originalStep);
        var deserializedStep = JsonSerializer.Deserialize<TestStep>(json);

        // Assert
        Assert.That(deserializedStep, Is.Not.Null);
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

        public override Task HandleAsync(
            Uri url,
            TestSettings settings,
            Components.TestContext context,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
