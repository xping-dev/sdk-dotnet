/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services;

#pragma warning disable CA1305 // Specify IFormatProvider
#pragma warning disable CA1307 // Specify StringComparison
#pragma warning disable CA1308 // ToLowerInvariant

namespace Xping.Sdk.Core.Tests.Models;

using System;
using Xunit;
using Xping.Sdk.Core.Models;

/// <summary>
/// Tests for TestIdentityGenerator to ensure stable, deterministic test identification.
/// </summary>
public sealed class TestIdentityGeneratorTests
{
    #region Basic Generation Tests

    [Fact]
    public void Generate_WithValidInputs_ReturnsValidIdentity()
    {
        // Arrange
        var fqn = "MyApp.Tests.CalculatorTests.Add_ReturnsSum";
        var assembly = "MyApp.Tests";

        // Act
        var identity = TestIdentityGenerator.Generate(fqn, assembly);

        // Assert
        Assert.NotNull(identity);
        Assert.NotEmpty(identity.TestId);
        Assert.Equal(64, identity.TestId.Length); // SHA256 produces 64 hex characters
        Assert.Equal(fqn, identity.FullyQualifiedName);
        Assert.Equal(assembly, identity.Assembly);
        Assert.Equal("MyApp.Tests", identity.Namespace);
        Assert.Equal("CalculatorTests", identity.ClassName);
        Assert.Equal("Add_ReturnsSum", identity.MethodName);
    }

    [Fact]
    public void Generate_WithNullFQN_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            TestIdentityGenerator.Generate(null!, "Assembly"));
    }

    [Fact]
    public void Generate_WithEmptyFQN_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            TestIdentityGenerator.Generate("", "Assembly"));
    }

    [Fact]
    public void Generate_WithNullAssembly_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            TestIdentityGenerator.Generate("Namespace.Class.Method", null!));
    }

    [Fact]
    public void Generate_WithInvalidFQNFormat_ThrowsArgumentException()
    {
        // Arrange - FQN with only one part (no class.method)
        var invalidFqn = "SinglePart";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            TestIdentityGenerator.Generate(invalidFqn, "Assembly"));
    }

    #endregion

    #region Hash Stability Tests

    [Fact]
    public void GenerateTestId_SameInputs_ProducesSameHash()
    {
        // Arrange
        var fqn = "MyApp.Tests.CalculatorTests.Add_ReturnsSum";

        // Act
        var hash1 = TestIdentityGenerator.GenerateTestId(fqn);
        var hash2 = TestIdentityGenerator.GenerateTestId(fqn);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GenerateTestId_DifferentInputs_ProducesDifferentHash()
    {
        // Arrange
        var fqn1 = "MyApp.Tests.CalculatorTests.Add_ReturnsSum";
        var fqn2 = "MyApp.Tests.CalculatorTests.Subtract_ReturnsDifference";

        // Act
        var hash1 = TestIdentityGenerator.GenerateTestId(fqn1);
        var hash2 = TestIdentityGenerator.GenerateTestId(fqn2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateTestId_WithParameters_ProducesDifferentHashThanWithout()
    {
        // Arrange
        var fqn = "MyApp.Tests.CalculatorTests.Add_ReturnsSum";

        // Act
        var hashWithoutParams = TestIdentityGenerator.GenerateTestId(fqn);
        var hashWithParams = TestIdentityGenerator.GenerateTestId(fqn, "2,3,5");

        // Assert
        Assert.NotEqual(hashWithoutParams, hashWithParams);
    }

    [Fact]
    public void Generate_CalledMultipleTimes_ProducesStableIdentity()
    {
        // Arrange
        var fqn = "MyApp.Tests.CalculatorTests.Add_ReturnsSum";
        var assembly = "MyApp.Tests";
        var parameters = new object[] { 2, 3, 5 };

        // Act
        var identity1 = TestIdentityGenerator.Generate(fqn, assembly, parameters);
        var identity2 = TestIdentityGenerator.Generate(fqn, assembly, parameters);
        var identity3 = TestIdentityGenerator.Generate(fqn, assembly, parameters);

        // Assert
        Assert.Equal(identity1.TestId, identity2.TestId);
        Assert.Equal(identity2.TestId, identity3.TestId);
        Assert.Equal(identity1.ParameterHash, identity2.ParameterHash);
        Assert.Equal(identity2.ParameterHash, identity3.ParameterHash);
    }

    #endregion

    #region Parameterized Test Handling

    [Theory]
    [InlineData(2, 3, 5)]
    [InlineData(10, -5, 5)]
    [InlineData(0, 0, 0)]
    public void Generate_WithDifferentParameters_ProducesDifferentIdentities(int a, int b, int expected)
    {
        // Arrange
        var fqn = "MyApp.Tests.CalculatorTests.Add_ReturnsSum";
        var assembly = "MyApp.Tests";
        var parameters = new object[] { a, b, expected };

        // Act
        var identity = TestIdentityGenerator.Generate(fqn, assembly, parameters);

        // Assert
        Assert.NotNull(identity.ParameterHash);
        Assert.Contains(a.ToString(), identity.ParameterHash);
        Assert.Contains(b.ToString(), identity.ParameterHash);
        Assert.Contains(expected.ToString(), identity.ParameterHash);
    }

    [Fact]
    public void GenerateParameterHash_WithNullArray_ReturnsEmptyString()
    {
        // Act
        var hash = TestIdentityGenerator.GenerateParameterHash(null!);

        // Assert
        Assert.Equal(string.Empty, hash);
    }

    [Fact]
    public void GenerateParameterHash_WithEmptyArray_ReturnsEmptyString()
    {
        // Act
        var hash = TestIdentityGenerator.GenerateParameterHash(Array.Empty<object>());

        // Assert
        Assert.Equal(string.Empty, hash);
    }

    [Fact]
    public void GenerateParameterHash_WithIntegers_ReturnsCommaSeparatedString()
    {
        // Arrange
        var parameters = new object[] { 2, 3, 5 };

        // Act
        var hash = TestIdentityGenerator.GenerateParameterHash(parameters);

        // Assert
        Assert.Equal("2,3,5", hash);
    }

    [Fact]
    public void GenerateParameterHash_WithStrings_ReturnsCommaSeparatedString()
    {
        // Arrange
        var parameters = new object[] { "hello", "world", "test" };

        // Act
        var hash = TestIdentityGenerator.GenerateParameterHash(parameters);

        // Assert
        Assert.Equal("hello,world,test", hash);
    }

    [Fact]
    public void GenerateParameterHash_WithNullValue_ReturnsNullInString()
    {
        // Arrange
        var parameters = new object?[] { "hello", null, 42 };

        // Act
        var hash = TestIdentityGenerator.GenerateParameterHash(parameters!);

        // Assert
        Assert.Equal("hello,null,42", hash);
    }

    [Fact]
    public void GenerateParameterHash_WithBooleans_ReturnsLowercaseTrueFalse()
    {
        // Arrange
        var parameters = new object[] { true, false };

        // Act
        var hash = TestIdentityGenerator.GenerateParameterHash(parameters);

        // Assert
        Assert.Equal("true,false", hash);
    }

    [Fact]
    public void GenerateParameterHash_WithFloatingPoint_UsesInvariantCulture()
    {
        // Arrange
        var parameters = new object[] { 3.14f, 2.718281828459045 };

        // Act
        var hash = TestIdentityGenerator.GenerateParameterHash(parameters);

        // Assert
        // Float uses G9 format, double uses G17 format
        Assert.StartsWith("3.14", hash);
        Assert.Contains("2.71828", hash);
    }

    [Fact]
    public void GenerateParameterHash_WithDateTime_UsesISO8601Format()
    {
        // Arrange
        var dt = new DateTime(2025, 1, 15, 14, 30, 45, DateTimeKind.Utc);
        var parameters = new object[] { dt };

        // Act
        var hash = TestIdentityGenerator.GenerateParameterHash(parameters);

        // Assert
        Assert.Equal("2025-01-15T14:30:45.0000000Z", hash);
    }

    [Fact]
    public void GenerateParameterHash_WithGuid_UsesDashFormat()
    {
        // Arrange
        var guid = new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var parameters = new object[] { guid };

        // Act
        var hash = TestIdentityGenerator.GenerateParameterHash(parameters);

        // Assert
        Assert.Equal("a1b2c3d4-e5f6-7890-abcd-ef1234567890", hash);
    }

    [Fact]
    public void GenerateParameterHash_WithArray_FormatsAsNestedBrackets()
    {
        // Arrange
        var parameters = new object[] { new[] { 1, 2, 3 }, "test" };

        // Act
        var hash = TestIdentityGenerator.GenerateParameterHash(parameters);

        // Assert
        Assert.Equal("[1,2,3],test", hash);
    }

    [Fact]
    public void GenerateParameterHash_WithMixedTypes_HandlesAllTypes()
    {
        // Arrange
        var parameters = new object[]
        {
            42,
            "hello",
            true,
            3.14,
            new[] { 1, 2 }
        };

        // Act
        var hash = TestIdentityGenerator.GenerateParameterHash(parameters);

        // Assert
        Assert.Contains("42", hash);
        Assert.Contains("hello", hash);
        Assert.Contains("true", hash);
        Assert.Contains("3.14", hash);
        Assert.Contains("[1,2]", hash);
    }

    #endregion

    #region FQN Parsing Tests

    [Fact]
    public void Generate_WithSimpleNamespace_ParsesCorrectly()
    {
        // Arrange
        var fqn = "Tests.Calculator.Add";
        var assembly = "Tests";

        // Act
        var identity = TestIdentityGenerator.Generate(fqn, assembly);

        // Assert
        Assert.Equal("Tests", identity.Namespace);
        Assert.Equal("Calculator", identity.ClassName);
        Assert.Equal("Add", identity.MethodName);
    }

    [Fact]
    public void Generate_WithNestedNamespace_ParsesCorrectly()
    {
        // Arrange
        var fqn = "MyApp.Tests.Unit.CalculatorTests.Add_ReturnsSum";
        var assembly = "MyApp.Tests";

        // Act
        var identity = TestIdentityGenerator.Generate(fqn, assembly);

        // Assert
        Assert.Equal("MyApp.Tests.Unit", identity.Namespace);
        Assert.Equal("CalculatorTests", identity.ClassName);
        Assert.Equal("Add_ReturnsSum", identity.MethodName);
    }

    [Fact]
    public void Generate_WithDeepNestedNamespace_ParsesCorrectly()
    {
        // Arrange
        var fqn = "Company.Product.Module.Tests.Unit.Integration.CalculatorTests.Add";
        var assembly = "Company.Product.Module.Tests";

        // Act
        var identity = TestIdentityGenerator.Generate(fqn, assembly);

        // Assert
        Assert.Equal("Company.Product.Module.Tests.Unit.Integration", identity.Namespace);
        Assert.Equal("CalculatorTests", identity.ClassName);
        Assert.Equal("Add", identity.MethodName);
    }

    #endregion

    #region Display Name Tests

    [Fact]
    public void Generate_WithoutParameters_UsesMethodNameAsDisplayName()
    {
        // Arrange
        var fqn = "Tests.Calculator.Add";
        var assembly = "Tests";

        // Act
        var identity = TestIdentityGenerator.Generate(fqn, assembly);

        // Assert
        Assert.Equal("Add", identity.DisplayName);
    }

    [Fact]
    public void Generate_WithParameters_CreatesDisplayNameWithParameters()
    {
        // Arrange
        var fqn = "Tests.Calculator.Add";
        var assembly = "Tests";
        var parameters = new object[] { 2, 3, 5 };

        // Act
        var identity = TestIdentityGenerator.Generate(fqn, assembly, parameters);

        // Assert
        Assert.Equal("Add(2,3,5)", identity.DisplayName);
    }

    [Fact]
    public void Generate_WithCustomDisplayName_UsesProvidedDisplayName()
    {
        // Arrange
        var fqn = "Tests.Calculator.Add";
        var assembly = "Tests";
        var parameters = new object[] { 2, 3, 5 };
        var customDisplayName = "Add: Should return 5 when adding 2 and 3";

        // Act
        var identity = TestIdentityGenerator.Generate(
            fqn,
            assembly,
            parameters,
            customDisplayName);

        // Assert
        Assert.Equal(customDisplayName, identity.DisplayName);
    }

    #endregion

    #region Source Location Tests

    [Fact]
    public void Generate_WithSourceInfo_StoresSourceFileAndLine()
    {
        // Arrange
        var fqn = "Tests.Calculator.Add";
        var assembly = "Tests";
        var sourceFile = "/path/to/CalculatorTests.cs";
        var sourceLine = 42;

        // Act
        var identity = TestIdentityGenerator.Generate(
            fqn,
            assembly,
            sourceFile: sourceFile,
            sourceLineNumber: sourceLine);

        // Assert
        Assert.Equal(sourceFile, identity.SourceFile);
        Assert.Equal(sourceLine, identity.SourceLineNumber);
    }

    [Fact]
    public void Generate_WithoutSourceInfo_LeavesSourceFieldsNull()
    {
        // Arrange
        var fqn = "Tests.Calculator.Add";
        var assembly = "Tests";

        // Act
        var identity = TestIdentityGenerator.Generate(fqn, assembly);

        // Assert
        Assert.Null(identity.SourceFile);
        Assert.Null(identity.SourceLineNumber);
    }

    #endregion

    #region Edge Cases and Special Characters

    [Fact]
    public void GenerateParameterHash_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var parameters = new object[] { "hello,world", "test\"quote", "path\\slash" };

        // Act
        var hash = TestIdentityGenerator.GenerateParameterHash(parameters);

        // Assert
        Assert.Contains("hello,world", hash);
        Assert.Contains("test\"quote", hash);
        Assert.Contains("path\\slash", hash);
    }

    [Fact]
    public void GenerateParameterHash_WithUnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        var parameters = new object[] { "Hello 世界", "Тест", "🚀" };

        // Act
        var hash = TestIdentityGenerator.GenerateParameterHash(parameters);

        // Assert
        Assert.Contains("Hello 世界", hash);
        Assert.Contains("Тест", hash);
        Assert.Contains("🚀", hash);
    }

    [Fact]
    public void GenerateParameterHash_WithVeryLargeNumber_HandlesCorrectly()
    {
        // Arrange
        var parameters = new object[] { long.MaxValue, ulong.MaxValue };

        // Act
        var hash = TestIdentityGenerator.GenerateParameterHash(parameters);

        // Assert
        Assert.Contains(long.MaxValue.ToString(), hash);
        Assert.Contains(ulong.MaxValue.ToString(), hash);
    }

    [Fact]
    public void Generate_WithVeryLongNamespace_HandlesCorrectly()
    {
        // Arrange
        var fqn = "Level1.Level2.Level3.Level4.Level5.Level6.Level7.Level8.TestClass.TestMethod";
        var assembly = "TestAssembly";

        // Act
        var identity = TestIdentityGenerator.Generate(fqn, assembly);

        // Assert
        Assert.Equal("Level1.Level2.Level3.Level4.Level5.Level6.Level7.Level8", identity.Namespace);
        Assert.Equal("TestClass", identity.ClassName);
        Assert.Equal("TestMethod", identity.MethodName);
        Assert.NotEmpty(identity.TestId);
    }

    #endregion

    #region Cross-Platform Consistency Tests

    [Fact]
    public void GenerateTestId_ProducesLowercaseHash()
    {
        // Arrange
        var fqn = "Tests.Calculator.Add";

        // Act
        var hash = TestIdentityGenerator.GenerateTestId(fqn);

        // Assert
        Assert.Equal(hash.ToLowerInvariant(), hash);
    }

    [Fact]
    public void GenerateParameterHash_WithNumericTypes_UsesInvariantCulture()
    {
        // Arrange - Use decimal which has culture-specific formatting
        var parameters = new object[] { 1234.56m };

        // Act
        var hash = TestIdentityGenerator.GenerateParameterHash(parameters);

        // Assert
        // Should use "." as decimal separator regardless of current culture
        Assert.Equal("1234.56", hash);
    }

    #endregion

    #region Real-World Scenario Tests

    [Fact]
    public void Generate_XUnitTheoryScenario_ProducesStableIdentity()
    {
        // Arrange - Simulating xUnit [Theory] test
        var fqn = "MyApp.Tests.CalculatorTests.Add_ReturnsExpectedSum";
        var assembly = "MyApp.Tests";
        var testCase1 = new object[] { 2, 3, 5 };
        var testCase2 = new object[] { 10, -5, 5 };

        // Act
        var identity1 = TestIdentityGenerator.Generate(fqn, assembly, testCase1);
        var identity2 = TestIdentityGenerator.Generate(fqn, assembly, testCase2);

        // Assert
        Assert.NotEqual(identity1.TestId, identity2.TestId);
        Assert.Equal("2,3,5", identity1.ParameterHash);
        Assert.Equal("10,-5,5", identity2.ParameterHash);
    }

    [Fact]
    public void Generate_NUnitTestCaseScenario_ProducesStableIdentity()
    {
        // Arrange - Simulating NUnit [TestCase] test
        var fqn = "MyApp.Tests.CalculatorTests.Divide";
        var assembly = "MyApp.Tests";
        var testCase1 = new object[] { 10, 2, 5 };
        var testCase2 = new object[] { 10, 0 }; // Division by zero case

        // Act
        var identity1 = TestIdentityGenerator.Generate(fqn, assembly, testCase1);
        var identity2 = TestIdentityGenerator.Generate(fqn, assembly, testCase2);

        // Assert
        Assert.NotEqual(identity1.TestId, identity2.TestId);
        Assert.Equal("10,2,5", identity1.ParameterHash);
        Assert.Equal("10,0", identity2.ParameterHash);
    }

    [Fact]
    public void Generate_MSTestDataRowScenario_ProducesStableIdentity()
    {
        // Arrange - Simulating MSTest [DataRow] test
        var fqn = "MyApp.Tests.StringTests.Contains_ReturnsTrue";
        var assembly = "MyApp.Tests";
        var testCase1 = new object[] { "hello world", "world", true };
        var testCase2 = new object[] { "hello world", "foo", false };

        // Act
        var identity1 = TestIdentityGenerator.Generate(fqn, assembly, testCase1);
        var identity2 = TestIdentityGenerator.Generate(fqn, assembly, testCase2);

        // Assert
        Assert.NotEqual(identity1.TestId, identity2.TestId);
        Assert.Equal("hello world,world,true", identity1.ParameterHash);
        Assert.Equal("hello world,foo,false", identity2.ParameterHash);
    }

    #endregion
}
