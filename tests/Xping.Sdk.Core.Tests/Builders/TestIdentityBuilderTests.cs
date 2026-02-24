/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;

namespace Xping.Sdk.Core.Tests.Builders;

public sealed class TestIdentityBuilderTests
{
    // ---------------------------------------------------------------------------
    // Build — defaults
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_ShouldReturnIdentity_WithEmptyStringDefaults()
    {
        var identity = new TestIdentityBuilder().Build();

        Assert.Equal(string.Empty, identity.TestFingerprint);
        Assert.Equal(string.Empty, identity.FullyQualifiedName);
        Assert.Equal(string.Empty, identity.Assembly);
        Assert.Equal(string.Empty, identity.Namespace);
        Assert.Equal(string.Empty, identity.ClassName);
        Assert.Equal(string.Empty, identity.MethodName);
        Assert.Equal(string.Empty, identity.DisplayName);
        Assert.Null(identity.ParameterHash);
        Assert.Null(identity.SourceFile);
        Assert.Null(identity.SourceLineNumber);
    }

    // ---------------------------------------------------------------------------
    // With* methods
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithTestId_ShouldSetTestId()
    {
        var identity = new TestIdentityBuilder().WithTestFingerprint("stable-id-abc").Build();
        Assert.Equal("stable-id-abc", identity.TestFingerprint);
    }

    [Fact]
    public void WithFullyQualifiedName_ShouldSetFQN()
    {
        var identity = new TestIdentityBuilder().WithFullyQualifiedName("Ns.Class.Method").Build();
        Assert.Equal("Ns.Class.Method", identity.FullyQualifiedName);
    }

    [Fact]
    public void WithAssembly_ShouldSetAssembly()
    {
        var identity = new TestIdentityBuilder().WithAssembly("MyAssembly").Build();
        Assert.Equal("MyAssembly", identity.Assembly);
    }

    [Fact]
    public void WithNamespace_ShouldSetNamespace()
    {
        var identity = new TestIdentityBuilder().WithNamespace("My.Namespace").Build();
        Assert.Equal("My.Namespace", identity.Namespace);
    }

    [Fact]
    public void WithClassName_ShouldSetClassName()
    {
        var identity = new TestIdentityBuilder().WithClassName("MyTestClass").Build();
        Assert.Equal("MyTestClass", identity.ClassName);
    }

    [Fact]
    public void WithMethodName_ShouldSetMethodName()
    {
        var identity = new TestIdentityBuilder().WithMethodName("ShouldDoSomething").Build();
        Assert.Equal("ShouldDoSomething", identity.MethodName);
    }

    [Fact]
    public void WithDisplayName_ShouldSetDisplayName()
    {
        var identity = new TestIdentityBuilder().WithDisplayName("ShouldDoSomething (param=1)").Build();
        Assert.Equal("ShouldDoSomething (param=1)", identity.DisplayName);
    }

    [Fact]
    public void WithParameterHash_ShouldSetParameterHash()
    {
        var identity = new TestIdentityBuilder().WithParameterHash("abc123").Build();
        Assert.Equal("abc123", identity.ParameterHash);
    }

    [Fact]
    public void WithParameterHash_Null_ShouldSetNull()
    {
        var identity = new TestIdentityBuilder().WithParameterHash("abc").WithParameterHash(null).Build();
        Assert.Null(identity.ParameterHash);
    }

    [Fact]
    public void WithSourceFile_ShouldSetSourceFile()
    {
        var identity = new TestIdentityBuilder().WithSourceFile("/src/MyTest.cs").Build();
        Assert.Equal("/src/MyTest.cs", identity.SourceFile);
    }

    [Fact]
    public void WithSourceFile_Null_ShouldSetNull()
    {
        var identity = new TestIdentityBuilder().WithSourceFile("/file.cs").WithSourceFile(null).Build();
        Assert.Null(identity.SourceFile);
    }

    [Fact]
    public void WithSourceLineNumber_ShouldSetLineNumber()
    {
        var identity = new TestIdentityBuilder().WithSourceLineNumber(42).Build();
        Assert.Equal(42, identity.SourceLineNumber);
    }

    [Fact]
    public void WithSourceLineNumber_Null_ShouldSetNull()
    {
        var identity = new TestIdentityBuilder().WithSourceLineNumber(10).WithSourceLineNumber(null).Build();
        Assert.Null(identity.SourceLineNumber);
    }

    // ---------------------------------------------------------------------------
    // Reset
    // ---------------------------------------------------------------------------

    [Fact]
    public void Reset_ShouldRestoreAllDefaults()
    {
        var builder = new TestIdentityBuilder()
            .WithTestFingerprint("id")
            .WithFullyQualifiedName("Ns.C.M")
            .WithAssembly("Asm")
            .WithNamespace("Ns")
            .WithClassName("C")
            .WithMethodName("M")
            .WithDisplayName("M(1)")
            .WithParameterHash("hash")
            .WithSourceFile("file.cs")
            .WithSourceLineNumber(99);

        builder.Reset();
        var identity = builder.Build();

        Assert.Equal(string.Empty, identity.TestFingerprint);
        Assert.Equal(string.Empty, identity.FullyQualifiedName);
        Assert.Equal(string.Empty, identity.Assembly);
        Assert.Null(identity.ParameterHash);
        Assert.Null(identity.SourceFile);
        Assert.Null(identity.SourceLineNumber);
    }

    [Fact]
    public void Reset_ShouldReturnBuilderInstance_ForChaining()
    {
        var builder = new TestIdentityBuilder();
        Assert.Same(builder, builder.Reset());
    }

    // ---------------------------------------------------------------------------
    // Fluent chain
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_ShouldSupportFullFluentChain()
    {
        var identity = new TestIdentityBuilder()
            .WithTestFingerprint("test-id")
            .WithFullyQualifiedName("Company.Tests.MyClass.ShouldPass")
            .WithAssembly("Company.Tests")
            .WithNamespace("Company.Tests")
            .WithClassName("MyClass")
            .WithMethodName("ShouldPass")
            .WithDisplayName("ShouldPass(42)")
            .WithParameterHash("42")
            .WithSourceFile("MyClass.cs")
            .WithSourceLineNumber(15)
            .Build();

        Assert.Equal("test-id", identity.TestFingerprint);
        Assert.Equal("Company.Tests.MyClass.ShouldPass", identity.FullyQualifiedName);
        Assert.Equal("Company.Tests", identity.Assembly);
        Assert.Equal("Company.Tests", identity.Namespace);
        Assert.Equal("MyClass", identity.ClassName);
        Assert.Equal("ShouldPass", identity.MethodName);
        Assert.Equal("ShouldPass(42)", identity.DisplayName);
        Assert.Equal("42", identity.ParameterHash);
        Assert.Equal("MyClass.cs", identity.SourceFile);
        Assert.Equal(15, identity.SourceLineNumber);
    }
}
