/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services;

namespace Xping.Sdk.Core.Tests.Models;

using Xunit;
using Xping.Sdk.Core.Models;

/// <summary>
/// Tests for TestIdentity model to ensure proper data storage and retrieval.
/// </summary>
public sealed class TestIdentityTests
{
    [Fact]
    public void TestIdentity_CanBeInstantiated()
    {
        // Act
        var identity = new TestIdentity();

        // Assert
        Assert.NotNull(identity);
        Assert.Equal(string.Empty, identity.TestId);
        Assert.Equal(string.Empty, identity.FullyQualifiedName);
        Assert.Equal(string.Empty, identity.Assembly);
        Assert.Equal(string.Empty, identity.Namespace);
        Assert.Equal(string.Empty, identity.ClassName);
        Assert.Equal(string.Empty, identity.MethodName);
        Assert.Null(identity.ParameterHash);
        Assert.Null(identity.DisplayName);
        Assert.Null(identity.SourceFile);
        Assert.Null(identity.SourceLineNumber);
    }

    [Fact]
    public void TestIdentity_PropertiesCanBeSet()
    {
        // Arrange
        var identity = new TestIdentity();

        // Act
        identity.TestId = "test-id-123";
        identity.FullyQualifiedName = "Namespace.Class.Method";
        identity.Assembly = "TestAssembly";
        identity.Namespace = "Namespace";
        identity.ClassName = "Class";
        identity.MethodName = "Method";
        identity.ParameterHash = "1,2,3";
        identity.DisplayName = "Test Display Name";
        identity.SourceFile = "/path/to/test.cs";
        identity.SourceLineNumber = 42;

        // Assert
        Assert.Equal("test-id-123", identity.TestId);
        Assert.Equal("Namespace.Class.Method", identity.FullyQualifiedName);
        Assert.Equal("TestAssembly", identity.Assembly);
        Assert.Equal("Namespace", identity.Namespace);
        Assert.Equal("Class", identity.ClassName);
        Assert.Equal("Method", identity.MethodName);
        Assert.Equal("1,2,3", identity.ParameterHash);
        Assert.Equal("Test Display Name", identity.DisplayName);
        Assert.Equal("/path/to/test.cs", identity.SourceFile);
        Assert.Equal(42, identity.SourceLineNumber);
    }

    [Fact]
    public void TestIdentity_WithoutParameters_HasNullParameterHash()
    {
        // Arrange
        var identity = TestIdentityGenerator.Generate(
            "Tests.Calculator.Add",
            "Tests");

        // Assert
        Assert.Null(identity.ParameterHash);
    }

    [Fact]
    public void TestIdentity_WithParameters_HasPopulatedParameterHash()
    {
        // Arrange
        var identity = TestIdentityGenerator.Generate(
            "Tests.Calculator.Add",
            "Tests",
            new object[] { 2, 3, 5 });

        // Assert
        Assert.NotNull(identity.ParameterHash);
        Assert.Equal("2,3,5", identity.ParameterHash);
    }

    [Fact]
    public void TestIdentity_TestIdIsStableHash()
    {
        // Arrange
        var fqn = "Tests.Calculator.Add";
        var assembly = "Tests";

        // Act
        var identity1 = TestIdentityGenerator.Generate(fqn, assembly);
        var identity2 = TestIdentityGenerator.Generate(fqn, assembly);

        // Assert
        Assert.Equal(identity1.TestId, identity2.TestId);
        Assert.Equal(64, identity1.TestId.Length); // SHA256 is 64 hex characters
    }
}
