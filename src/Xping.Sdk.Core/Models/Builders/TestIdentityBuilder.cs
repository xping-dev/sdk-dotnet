/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Models.Builders;

/// <summary>
/// Builder for constructing immutable <see cref="TestIdentity"/> instances.
/// </summary>
public sealed class TestIdentityBuilder
{
    private string _testFingerprint;
    private string _fullyQualifiedName;
    private string _assembly;
    private string _namespace;
    private string _className;
    private string _methodName;
    private string _displayName;
    private string? _parameterHash;
    private string? _sourceFile;
    private int? _sourceLineNumber;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestIdentityBuilder"/> class.
    /// </summary>
    public TestIdentityBuilder()
    {
        _testFingerprint = string.Empty;
        _fullyQualifiedName = string.Empty;
        _assembly = string.Empty;
        _namespace = string.Empty;
        _className = string.Empty;
        _methodName = string.Empty;
        _displayName = string.Empty;
    }

    /// <summary>
    /// Sets the test ID.
    /// </summary>
    public TestIdentityBuilder WithTestId(string testId)
    {
        _testFingerprint = testId;
        return this;
    }

    /// <summary>
    /// Sets the fully qualified name.
    /// </summary>
    public TestIdentityBuilder WithFullyQualifiedName(string fullyQualifiedName)
    {
        _fullyQualifiedName = fullyQualifiedName;
        return this;
    }

    /// <summary>
    /// Sets the assembly name.
    /// </summary>
    public TestIdentityBuilder WithAssembly(string assembly)
    {
        _assembly = assembly;
        return this;
    }

    /// <summary>
    /// Sets the namespace.
    /// </summary>
    public TestIdentityBuilder WithNamespace(string @namespace)
    {
        _namespace = @namespace;
        return this;
    }

    /// <summary>
    /// Sets the class name.
    /// </summary>
    public TestIdentityBuilder WithClassName(string className)
    {
        _className = className;
        return this;
    }

    /// <summary>
    /// Sets the method name.
    /// </summary>
    public TestIdentityBuilder WithMethodName(string methodName)
    {
        _methodName = methodName;
        return this;
    }

    /// <summary>
    /// Sets the display name.
    /// </summary>
    public TestIdentityBuilder WithDisplayName(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    /// <summary>
    /// Sets the parameter hash for parameterized tests.
    /// </summary>
    public TestIdentityBuilder WithParameterHash(string? parameterHash)
    {
        _parameterHash = parameterHash;
        return this;
    }

    /// <summary>
    /// Sets the source file location.
    /// </summary>
    public TestIdentityBuilder WithSourceFile(string? sourceFile)
    {
        _sourceFile = sourceFile;
        return this;
    }

    /// <summary>
    /// Sets the source line number.
    /// </summary>
    public TestIdentityBuilder WithSourceLineNumber(int? sourceLineNumber)
    {
        _sourceLineNumber = sourceLineNumber;
        return this;
    }

    /// <summary>
    /// Builds an immutable <see cref="TestIdentity"/> instance.
    /// </summary>
    public TestIdentity Build()
    {
        return new TestIdentity(
            testFingerprint: _testFingerprint,
            fullyQualifiedName: _fullyQualifiedName,
            assembly: _assembly,
            @namespace: _namespace,
            className: _className,
            methodName: _methodName,
            displayName: _displayName,
            parameterHash: _parameterHash,
            sourceFile: _sourceFile,
            sourceLineNumber: _sourceLineNumber);
    }

    /// <summary>
    /// Resets the builder to its initial state.
    /// </summary>
    public TestIdentityBuilder Reset()
    {
        _testFingerprint = string.Empty;
        _fullyQualifiedName = string.Empty;
        _assembly = string.Empty;
        _namespace = string.Empty;
        _className = string.Empty;
        _methodName = string.Empty;
        _displayName = string.Empty;
        _parameterHash = null;
        _sourceFile = null;
        _sourceLineNumber = null;
        return this;
    }
}
