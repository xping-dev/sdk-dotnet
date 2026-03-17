/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Xping.Sdk.Core.Attributes;
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
    // Generate with pinned testFingerprint
    // ---------------------------------------------------------------------------

    [Fact]
    public void Generate_WhenTestFingerprintProvided_UsesPinnedValueDirectly()
    {
        // Arrange
        var generator = BuildGenerator();
        const string pinnedValue = "my-stable-id";

        // Act
        var identity = generator.Generate(ValidFqn, ValidAssembly, testFingerprint: pinnedValue);

        // Assert
        Assert.Equal(pinnedValue, identity.TestFingerprint);
    }

    [Fact]
    public void Generate_WhenTestFingerprintProvided_DoesNotMatchSHA256OfFQN()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var withPin = generator.Generate(ValidFqn, ValidAssembly, testFingerprint: "custom-id").TestFingerprint;
        var withoutPin = generator.Generate(ValidFqn, ValidAssembly).TestFingerprint;

        // Assert — the pinned value is not the same as the computed SHA256
        Assert.NotEqual(withPin, withoutPin);
    }

    [Fact]
    public void Generate_WhenTestFingerprintIsNull_FallsBackToSHA256()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var fromGenerate = generator.Generate(ValidFqn, ValidAssembly, testFingerprint: null).TestFingerprint;
        var fromDirectHash = generator.GenerateTestFingerprint(ValidFqn);

        // Assert
        Assert.Equal(fromDirectHash, fromGenerate);
    }

    [Fact]
    public void Generate_WhenTestFingerprintIsEmpty_FallsBackToSHA256()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var fromGenerate = generator.Generate(ValidFqn, ValidAssembly, testFingerprint: string.Empty).TestFingerprint;
        var fromDirectHash = generator.GenerateTestFingerprint(ValidFqn);

        // Assert
        Assert.Equal(fromDirectHash, fromGenerate);
    }

    [Fact]
    public void Generate_WhenTestFingerprintIsWhitespace_FallsBackToSHA256()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act
        var fromGenerate = generator.Generate(ValidFqn, ValidAssembly, testFingerprint: "   ").TestFingerprint;
        var fromDirectHash = generator.GenerateTestFingerprint(ValidFqn);

        // Assert
        Assert.Equal(fromDirectHash, fromGenerate);
    }

    [Fact]
    public void Generate_PinnedFingerprint_WithParameters_ProducesUniqueValuesPerVariant()
    {
        // Arrange
        var generator = BuildGenerator();
        const string pinnedValue = "fixed-v1";

        // Act
        var params1 = generator.Generate(ValidFqn, ValidAssembly, parameters: [1, 2], testFingerprint: pinnedValue).TestFingerprint;
        var params2 = generator.Generate(ValidFqn, ValidAssembly, parameters: [3, 4], testFingerprint: pinnedValue).TestFingerprint;

        // Assert — different parameter sets produce different fingerprints even with the same pin
        Assert.NotEqual(params1, params2);
        Assert.StartsWith(pinnedValue + ":", params1, StringComparison.Ordinal);
        Assert.StartsWith(pinnedValue + ":", params2, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_PinnedFingerprint_IsImmutableAcrossNameChanges()
    {
        // Arrange
        var generator = BuildGenerator();
        const string pinnedValue = "fixed-v1";

        // Act
        var identity1 = generator.Generate("A.B.OldTest", ValidAssembly, testFingerprint: pinnedValue).TestFingerprint;
        var identity2 = generator.Generate("X.Y.NewTest", ValidAssembly, testFingerprint: pinnedValue).TestFingerprint;

        // Assert
        Assert.Equal(pinnedValue, identity1);
        Assert.Equal(pinnedValue, identity2);
    }

    [Fact]
    public void Generate_PinnedFingerprint_DoesNotAffectOtherIdentityFields()
    {
        // Arrange
        var generator = BuildGenerator();
        var parameters = new object[] { 42 };

        // Act
        var identity = generator.Generate(
            ValidFqn, ValidAssembly,
            parameters: parameters,
            displayName: "Custom Name",
            sourceFile: "file.cs",
            sourceLineNumber: 10,
            testFingerprint: "my-pin");

        // Assert — TestFingerprint = pin + ":" + paramHash; everything else is normal
        Assert.Equal("my-pin:42", identity.TestFingerprint);
        Assert.Equal(ValidFqn, identity.FullyQualifiedName);
        Assert.Equal(ValidAssembly, identity.Assembly);
        Assert.Equal("MyNamespace", identity.Namespace);
        Assert.Equal("MyClass", identity.ClassName);
        Assert.Equal("MyTest", identity.MethodName);
        Assert.Equal("Custom Name", identity.DisplayName);
        Assert.NotNull(identity.ParameterHash);
        Assert.Equal("file.cs", identity.SourceFile);
        Assert.Equal(10, identity.SourceLineNumber);
    }

    [Fact]
    public void Generate_PinnedFingerprintAcceptsArbitraryFormat()
    {
        // Arrange
        var generator = BuildGenerator();

        // Act + Assert — UUID, slug, and 64-char SHA256-like hex are all accepted as-is
        var uuid = "c4e12f97-1a23-4b56-8c9d-0e1f23456789";
        Assert.Equal(uuid,
            generator.Generate(ValidFqn, ValidAssembly, testFingerprint: uuid).TestFingerprint);

        var slug = "login-happy-path-v1";
        Assert.Equal(slug,
            generator.Generate(ValidFqn, ValidAssembly, testFingerprint: slug).TestFingerprint);

        var hexLike = new string('a', 64);
        Assert.Equal(hexLike,
            generator.Generate(ValidFqn, ValidAssembly, testFingerprint: hexLike).TestFingerprint);
    }

    [Fact]
    public void Generate_PinnedFingerprint_WithParameters_AppendsSeparatorAndParamHash()
    {
        // Arrange
        var generator = BuildGenerator();
        const string pin = "login-v1";
        var parameters = new object[] { "admin", true };

        // Act
        var identity = generator.Generate(ValidFqn, ValidAssembly, parameters: parameters, testFingerprint: pin);

        // Assert — fingerprint is "{pin}:{paramHash}", colon separator is unambiguous
        var expectedParamHash = generator.GenerateParameterHash(parameters);
        Assert.Equal($"{pin}:{expectedParamHash}", identity.TestFingerprint);
    }

    [Fact]
    public void Generate_PinnedFingerprint_WithNoParameters_UsesLiteralValue()
    {
        // Arrange
        var generator = BuildGenerator();
        const string pin = "login-v1";

        // Act
        var identity = generator.Generate(ValidFqn, ValidAssembly, testFingerprint: pin);

        // Assert — no parameters means the pinned value is used as-is, no suffix appended
        Assert.Equal(pin, identity.TestFingerprint);
    }

    // ---------------------------------------------------------------------------
    // XpingFingerprintAttribute slug validation
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("login-happy-path-v1")]
    [InlineData("checkout_smoke_42")]
    [InlineData("abc123")]
    [InlineData("A-B_C")]
    public void XpingFingerprintAttribute_WithValidSlug_DoesNotThrow(string fingerprint)
    {
        // Act & Assert — no exception
        var attr = new XpingFingerprintAttribute(fingerprint);
        Assert.Equal(fingerprint, attr.Fingerprint);
    }

    [Theory]
    [InlineData("login:v1")]          // colon — the reserved separator
    [InlineData("test.method")]       // period
    [InlineData("has space")]         // space
    [InlineData("uuid@host")]         // at-sign
    [InlineData("path/segment")]      // slash
    public void XpingFingerprintAttribute_WithInvalidSlugCharacters_ThrowsArgumentException(string fingerprint)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new XpingFingerprintAttribute(fingerprint));
    }

    [Fact]
    public void XpingFingerprintAttribute_ExactlyAtLengthLimit_DoesNotThrow()
    {
        // Arrange — 100 'a' characters (exactly at the limit)
        var fingerprint = new string('a', 100);

        // Act & Assert — no exception
        var attr = new XpingFingerprintAttribute(fingerprint);
        Assert.Equal(fingerprint, attr.Fingerprint);
    }

    [Fact]
    public void XpingFingerprintAttribute_OneCharacterOverLimit_ThrowsArgumentException()
    {
        // Arrange — 101 'a' characters (one over the limit)
        var fingerprint = new string('a', 101);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new XpingFingerprintAttribute(fingerprint));
    }

    [Fact]
    public void XpingFingerprintAttribute_ShortValue_DoesNotThrow()
    {
        // Arrange
        const string fingerprint = "short";

        // Act & Assert — no exception
        var attr = new XpingFingerprintAttribute(fingerprint);
        Assert.Equal(fingerprint, attr.Fingerprint);
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
