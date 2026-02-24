/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Tests.Helpers;

namespace Xping.Sdk.Core.Tests.Identity;

public sealed class TestIdentityGeneratorTests
{
    private static readonly object[] NullParameterArray = [null!];
    private static readonly object[] NestedArrayParameter = [new int[] { 1, 2 }];

    private static ITestIdentityGenerator BuildGenerator()
    {
        var provider = ServiceHelper.CreateCollectorServices().BuildServiceProvider();
        return provider.GetRequiredService<ITestIdentityGenerator>();
    }

    private const string ValidFqn = "MyNamespace.MyClass.MyTest";
    private const string ValidAssembly = "MyAssembly";

    // ---------------------------------------------------------------------------
    // Generate
    // ---------------------------------------------------------------------------

    [Fact]
    public void Generate_ShouldReturnIdentity_WithAllFieldsParsedFromFQN()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var identity = generator.Generate(ValidFqn, ValidAssembly);

        // Assert
        Assert.NotNull(identity);
        Assert.Equal(ValidFqn, identity.FullyQualifiedName);
        Assert.Equal(ValidAssembly, identity.Assembly);
        Assert.Equal("MyNamespace", identity.Namespace);
        Assert.Equal("MyClass", identity.ClassName);
        Assert.Equal("MyTest", identity.MethodName);
    }

    [Fact]
    public void Generate_ShouldProduceStableTestId_ForSameFQN()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var id1 = generator.Generate(ValidFqn, ValidAssembly).TestFingerprint;
        var id2 = generator.Generate(ValidFqn, ValidAssembly).TestFingerprint;

        // Assert
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void Generate_ShouldProduceDifferentTestIds_ForDifferentFQNs()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var id1 = generator.Generate("Namespace.ClassA.Test", ValidAssembly).TestFingerprint;
        var id2 = generator.Generate("Namespace.ClassB.Test", ValidAssembly).TestFingerprint;

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Generate_ShouldThrowArgumentNullException_ForNullFQN()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => generator.Generate(null!, ValidAssembly));
    }

    [Fact]
    public void Generate_ShouldThrowArgumentNullException_ForEmptyFQN()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => generator.Generate(string.Empty, ValidAssembly));
    }

    [Fact]
    public void Generate_ShouldThrowArgumentNullException_ForNullAssembly()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => generator.Generate(ValidFqn, null!));
    }

    [Fact]
    public void Generate_ShouldThrowArgumentException_ForInvalidFQN_SingleSegment()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => generator.Generate("NoDotsHere", ValidAssembly));
    }

    [Fact]
    public void Generate_ShouldIncludeParameterHash_ForParameterizedTests()
    {
        // Arrange
        var generator = BuildGenerator();
        var parameters = new object[] { 1, "hello" };

        // Act
        var identity = generator.Generate(ValidFqn, ValidAssembly, parameters);

        // Assert
        Assert.NotNull(identity.ParameterHash);
        Assert.NotEmpty(identity.ParameterHash);
    }

    [Fact]
    public void Generate_ShouldProduceDifferentTestId_ForDifferentParameters()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var withParams = generator.Generate(ValidFqn, ValidAssembly, parameters: [1, "a"]);
        var withoutParams = generator.Generate(ValidFqn, ValidAssembly);

        // Assert
        Assert.NotEqual(withParams.TestFingerprint, withoutParams.TestFingerprint);
    }

    [Fact]
    public void Generate_ShouldUseProvidedDisplayName()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var identity = generator.Generate(ValidFqn, ValidAssembly, displayName: "My Custom Name");

        // Assert
        Assert.Equal("My Custom Name", identity.DisplayName);
    }

    [Fact]
    public void Generate_ShouldDefaultDisplayName_ToMethodName_WhenNoParameters()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var identity = generator.Generate(ValidFqn, ValidAssembly);

        // Assert
        Assert.Equal("MyTest", identity.DisplayName);
    }

    [Fact]
    public void Generate_ShouldParseNestedNamespace_Correctly()
    {
        // Arrange
        var generator = BuildGenerator();
        const string fqn = "Company.Product.Tests.Feature.MyTestClass.ShouldDoSomething";

        // Act
        var identity = generator.Generate(fqn, ValidAssembly);

        // Assert
        Assert.Equal("Company.Product.Tests.Feature", identity.Namespace);
        Assert.Equal("MyTestClass", identity.ClassName);
        Assert.Equal("ShouldDoSomething", identity.MethodName);
    }

    // ---------------------------------------------------------------------------
    // GenerateTestFingerprint
    // ---------------------------------------------------------------------------

    [Fact]
    public void GenerateTestId_ShouldBeDeterministic()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var id1 = generator.GenerateTestFingerprint(ValidFqn);
        var id2 = generator.GenerateTestFingerprint(ValidFqn);

        // Assert
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void GenerateTestId_ShouldBe64CharHex()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var id = generator.GenerateTestFingerprint(ValidFqn);

        // Assert
        Assert.Equal(64, id.Length);
        Assert.True(id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }

    [Fact]
    public void GenerateTestId_ShouldDifferWhenParameterHashDiffers()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var idNoParam = generator.GenerateTestFingerprint(ValidFqn, parameterHash: null);
        var idWithParam = generator.GenerateTestFingerprint(ValidFqn, parameterHash: "abc");

        // Assert
        Assert.NotEqual(idNoParam, idWithParam);
    }

    [Fact]
    public void GenerateTestId_ShouldThrowArgumentNullException_ForNullFQN()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => generator.GenerateTestFingerprint(null!));
    }

    // ---------------------------------------------------------------------------
    // GenerateParameterHash
    // ---------------------------------------------------------------------------

    [Fact]
    public void GenerateParameterHash_ShouldReturnEmpty_ForZeroLengthArray()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var hash = generator.GenerateParameterHash([]);

        // Assert
        Assert.Equal(string.Empty, hash);
    }

    [Fact]
    public void GenerateParameterHash_ShouldFormatIntegers_WithInvariantCulture()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var hash = generator.GenerateParameterHash([1, 2, 3]);

        // Assert
        Assert.Equal("1,2,3", hash);
    }

    [Fact]
    public void GenerateParameterHash_ShouldFormatNull_AsNullString()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var hash = generator.GenerateParameterHash(NullParameterArray);

        // Assert
        Assert.Equal("null", hash);
    }

    [Fact]
    public void GenerateParameterHash_ShouldFormatArrays_WithBrackets()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var hash = generator.GenerateParameterHash(NestedArrayParameter);

        // Assert
        Assert.Equal("[1,2]", hash);
    }

    [Fact]
    public void GenerateParameterHash_ShouldBeDeterministic_ForSameParams()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var hash1 = generator.GenerateParameterHash(["hello", 42]);
        var hash2 = generator.GenerateParameterHash(["hello", 42]);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    // ---------------------------------------------------------------------------
    // GenerateErrorMessageHash / GenerateStackTraceHash
    // ---------------------------------------------------------------------------

    [Fact]
    public void GenerateErrorMessageHash_ShouldReturnNull_ForNullInput()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act & Assert
        Assert.Null(generator.GenerateErrorMessageHash(null));
    }

    [Fact]
    public void GenerateErrorMessageHash_ShouldReturnNull_ForEmptyInput()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act & Assert
        Assert.Null(generator.GenerateErrorMessageHash(string.Empty));
        Assert.Null(generator.GenerateErrorMessageHash("   "));
    }

    [Fact]
    public void GenerateErrorMessageHash_ShouldBeDeterministic()
    {
        // Arrange
        var generator = BuildGenerator();
        const string message = "Expected 1 but was 2.";

        // Act
        var h1 = generator.GenerateErrorMessageHash(message);
        var h2 = generator.GenerateErrorMessageHash(message);

        // Assert
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void GenerateErrorMessageHash_ShouldBeDifferent_ForDifferentMessages()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var h1 = generator.GenerateErrorMessageHash("Error A");
        var h2 = generator.GenerateErrorMessageHash("Error B");

        // Assert
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void GenerateStackTraceHash_ShouldReturnNull_ForNullInput()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act & Assert
        Assert.Null(generator.GenerateStackTraceHash(null));
    }

    [Fact]
    public void GenerateStackTraceHash_ShouldBeDeterministic()
    {
        // Arrange
        var generator = BuildGenerator();
        const string trace = "at SomeClass.SomeMethod() in file.cs:line 42";

        // Act
        var h1 = generator.GenerateStackTraceHash(trace);
        var h2 = generator.GenerateStackTraceHash(trace);

        // Assert
        Assert.Equal(h1, h2);
    }
}
