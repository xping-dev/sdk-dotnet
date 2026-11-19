/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Tests.Models.Executions;

public sealed class TestExecutionTests
{
    [Fact]
    public void WithoutStackTrace_DropsOnlyTheTraceAndMarksItOmitted()
    {
        TestExecution execution = new TestExecutionBuilder()
            .WithTestName("Fails")
            .WithOutcome(TestOutcome.Failed)
            .WithIdentity(new TestIdentity { Assembly = "MyApp.Tests", FullyQualifiedName = "MyApp.Tests.Fails" })
            .WithException("System.InvalidOperationException", "boom", "   at MyApp.Tests.Fails()")
            .WithErrorMessageHash("message-hash")
            .WithStackTraceHash("trace-hash")
            .WithTimeoutBudget(TimeSpan.FromSeconds(5), TimeoutBudgetSource.Declared)
            .WithFailureSite(FailureSite.TestBody, "Fails")
            .Build();

        TestExecution copy = execution.WithoutStackTrace();

        Assert.Null(copy.StackTrace);
        Assert.True(copy.StackTraceOmitted);

        // Every other property is carried over, including ones added after this test was written.
        foreach (PropertyInfo property in typeof(TestExecution).GetProperties())
        {
            if (property.Name is nameof(TestExecution.StackTrace) or nameof(TestExecution.StackTraceOmitted))
                continue;

            Assert.Equal(property.GetValue(execution), property.GetValue(copy));
        }
    }

    [Fact]
    public void WithoutStackTrace_LeavesAnExecutionWithoutATraceAsItIs()
    {
        TestExecution execution = new TestExecutionBuilder()
            .WithTestName("Passes")
            .WithOutcome(TestOutcome.Passed)
            .WithIdentity(new TestIdentity { Assembly = "MyApp.Tests", FullyQualifiedName = "MyApp.Tests.Passes" })
            .Build();

        TestExecution copy = execution.WithoutStackTrace();

        Assert.Same(execution, copy);
        Assert.False(copy.StackTraceOmitted);
    }
}
