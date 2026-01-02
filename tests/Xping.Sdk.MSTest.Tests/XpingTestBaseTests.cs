/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.MSTest.Tests;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core.Models;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Tests for XpingTestBase outcome mapping and metadata extraction.
/// </summary>
public class XpingTestBaseTests
{
    [Fact]
    public void MapOutcome_Passed_ReturnsPassed()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.Passed);

        Assert.Equal(TestOutcome.Passed, outcome);
    }

    [Fact]
    public void MapOutcome_Failed_ReturnsFailed()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.Failed);

        Assert.Equal(TestOutcome.Failed, outcome);
    }

    [Fact]
    public void MapOutcome_Inconclusive_ReturnsInconclusive()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.Inconclusive);

        Assert.Equal(TestOutcome.Inconclusive, outcome);
    }

    [Fact]
    public void MapOutcome_Timeout_ReturnsFailed()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.Timeout);

        Assert.Equal(TestOutcome.Failed, outcome);
    }

    [Fact]
    public void MapOutcome_Aborted_ReturnsFailed()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.Aborted);

        Assert.Equal(TestOutcome.Failed, outcome);
    }

    [Fact]
    public void MapOutcome_Unknown_ReturnsNotExecuted()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.Unknown);

        Assert.Equal(TestOutcome.NotExecuted, outcome);
    }

    [Fact]
    public void MapOutcome_NotRunnable_ReturnsNotExecuted()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.NotRunnable);

        Assert.Equal(TestOutcome.NotExecuted, outcome);
    }

    [Fact]
    public void MapOutcome_InProgress_ReturnsNotExecuted()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.InProgress);

        Assert.Equal(TestOutcome.NotExecuted, outcome);
    }

    // Helper method to invoke private MapOutcome using reflection
    private static TestOutcome InvokeMapOutcome(UnitTestOutcome outcome)
    {
        var method = typeof(XpingTestBase).GetMethod(
            "MapOutcome",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
        {
            throw new InvalidOperationException("MapOutcome method not found");
        }

        var result = method.Invoke(null, new object[] { outcome });
        return (TestOutcome)result!;
    }
}
