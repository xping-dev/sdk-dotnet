/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models.Executions;

/// <summary>
/// Immutable, stable, deterministic identity for a test that persists across:
/// - Different machines and environments (CI, local, Docker)
/// - Different .NET versions
/// - Code refactorings and minor formatting changes
/// - Parameterized test variations
/// </summary>
/// <remarks>
/// The TestId is a SHA256 hash computed from the fully qualified name and parameters,
/// ensuring the same test always generates the same ID regardless of execution context.
/// This enables reliable test tracking and historical analysis across test runs.
/// </remarks>
public sealed class TestIdentity
{
    /// <summary>
    /// Parameterless constructor for JSON deserialization.
    /// </summary>
    public TestIdentity()
    {
        TestId = string.Empty;
        FullyQualifiedName = string.Empty;
        Assembly = string.Empty;
        Namespace = string.Empty;
        ClassName = string.Empty;
        MethodName = string.Empty;
        DisplayName = string.Empty;
    }

    /// <summary>
    /// Internal constructor for manual construction.
    /// </summary>
    internal TestIdentity(
        string testId,
        string fullyQualifiedName,
        string assembly,
        string @namespace,
        string className,
        string methodName,
        string displayName,
        string? parameterHash,
        string? sourceFile,
        int? sourceLineNumber)
    {
        TestId = testId;
        FullyQualifiedName = fullyQualifiedName;
        Assembly = assembly;
        Namespace = @namespace;
        ClassName = className;
        MethodName = methodName;
        ParameterHash = parameterHash;
        DisplayName = displayName;
        SourceFile = sourceFile;
        SourceLineNumber = sourceLineNumber;
    }

    /// <summary>
    /// Gets the stable test identifier (SHA256 hash of FQN + parameters).
    /// This ID remains constant for the same test across all environments and runs.
    /// </summary>
    /// <remarks>
    /// Format: SHA256 hash as lowercase hex string (64 characters).
    /// Example: "a3f5e9c2d1b4a7f8e0c9d6b3a2f1e8d5c7b4a9f6e3d0c8b5a2f9e6d3c0b7a4f1"
    /// </remarks>
    public string TestId { get; private set; }

    /// <summary>
    /// Gets the fully qualified name of the test.
    /// Format: Namespace.ClassName.MethodName
    /// </summary>
    /// <remarks>
    /// Example: "MyApp.Tests.CalculatorTests.Add_ReturnSum"
    /// This may change during refactoring, but TestId remains stable.
    /// </remarks>
    public string FullyQualifiedName { get; private set; }

    /// <summary>
    /// Gets the assembly name where the test is defined.
    /// </summary>
    /// <remarks>
    /// Example: "MyApp.Tests"
    /// </remarks>
    public string Assembly { get; private set; }

    /// <summary>
    /// Gets the namespace where the test is defined.
    /// </summary>
    /// <remarks>
    /// Example: "MyApp.Tests"
    /// </remarks>
    public string Namespace { get; private set; }

    /// <summary>
    /// Gets the class name where the test method is defined.
    /// </summary>
    /// <remarks>
    /// Example: "CalculatorTests"
    /// </remarks>
    public string ClassName { get; private set; }

    /// <summary>
    /// Gets the test method name.
    /// </summary>
    /// <remarks>
    /// Example: "Add_ReturnSum"
    /// </remarks>
    public string MethodName { get; private set; }

    /// <summary>
    /// Gets the display name for human readability.
    /// This is the name shown in test runners and reports.
    /// </summary>
    /// <remarks>
    /// Example: "Add_ReturnSum(a: 2, b: 3, expected: 5)"
    /// May include parameter names and values for better readability.
    /// </remarks>
    public string DisplayName { get; private set; }

    /// <summary>
    /// Gets the parameter hash for parameterized tests.
    /// Null for non-parameterized tests.
    /// </summary>
    /// <remarks>
    /// For parameterized tests (xUnit [Theory], NUnit [TestCase], MSTest [DataRow]),
    /// this contains a stable hash of the parameter values.
    /// Example: "2,3,5" for Add_ReturnSum(2, 3, 5)
    ///
    /// Format: Comma-separated string representation of parameters.
    /// </remarks>
    public string? ParameterHash { get; private set; }

    /// <summary>
    /// Gets the source file path where the test is defined.
    /// Optional, useful for IDE navigation and debugging.
    /// </summary>
    public string? SourceFile { get; private set; }

    /// <summary>
    /// Gets the line number in the source file where the test begins.
    /// Optional, useful for IDE navigation and debugging.
    /// </summary>
    public int? SourceLineNumber { get; private set; }
}
