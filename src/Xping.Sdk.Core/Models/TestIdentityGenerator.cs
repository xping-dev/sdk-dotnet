/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Generates stable, deterministic test identities for tracking tests across runs,
/// environments, and code changes.
/// </summary>
/// <remarks>
/// This generator creates consistent TestId hashes that enable:
/// - Tracking the same test across different environments (CI, local, Docker)
/// - Monitoring test history across refactorings
/// - Identifying parameterized test variations
/// - Analyzing test reliability trends over time
/// 
/// The algorithm ensures:
/// - Deterministic output (same input always produces same hash)
/// - Cross-platform consistency (Windows, Linux, macOS)
/// - .NET version independence
/// - Collision resistance (using SHA256)
/// </remarks>
public static class TestIdentityGenerator
{
    /// <summary>
    /// Generates a complete test identity from test metadata.
    /// </summary>
    /// <param name="fullyQualifiedName">The fully qualified test name (Namespace.Class.Method).</param>
    /// <param name="assembly">The assembly name where the test is defined.</param>
    /// <param name="parameters">Optional parameters for parameterized tests.</param>
    /// <param name="displayName">Optional display name for human readability.</param>
    /// <param name="sourceFile">Optional source file path.</param>
    /// <param name="sourceLineNumber">Optional source line number.</param>
    /// <returns>A complete TestIdentity with stable TestId hash.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fullyQualifiedName or assembly is null.</exception>
    /// <exception cref="ArgumentException">Thrown when fullyQualifiedName is invalid format.</exception>
    public static TestIdentity Generate(
        string fullyQualifiedName,
        string assembly,
        object[]? parameters = null,
        string? displayName = null,
        string? sourceFile = null,
        int? sourceLineNumber = null)
    {
        if (string.IsNullOrWhiteSpace(fullyQualifiedName))
        {
            throw new ArgumentNullException(nameof(fullyQualifiedName));
        }

        if (string.IsNullOrWhiteSpace(assembly))
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        // Parse fully qualified name into components
        var parts = ParseFullyQualifiedName(fullyQualifiedName);

        // Generate parameter hash if parameters exist
        var parameterHash = parameters != null && parameters.Length > 0
            ? GenerateParameterHash(parameters)
            : null;

        // Generate stable test ID
        var testId = GenerateTestId(fullyQualifiedName, parameterHash);

        return new TestIdentity
        {
            TestId = testId,
            FullyQualifiedName = fullyQualifiedName,
            Assembly = assembly,
            Namespace = parts.Namespace,
            ClassName = parts.ClassName,
            MethodName = parts.MethodName,
            ParameterHash = parameterHash,
            DisplayName = displayName ?? GenerateDefaultDisplayName(parts.MethodName, parameters),
            SourceFile = sourceFile,
            SourceLineNumber = sourceLineNumber
        };
    }

    /// <summary>
    /// Generates a stable SHA256 hash for a test identifier.
    /// </summary>
    /// <param name="fullyQualifiedName">The fully qualified test name.</param>
    /// <param name="parameterHash">Optional parameter hash for parameterized tests.</param>
    /// <returns>A 64-character lowercase hex string (SHA256 hash).</returns>
    /// <remarks>
    /// The hash is computed from: "{fullyQualifiedName}|{parameterHash}"
    /// The pipe separator ensures distinct hashes for tests with/without parameters.
    /// </remarks>
    public static string GenerateTestId(string fullyQualifiedName, string? parameterHash = null)
    {
        if (string.IsNullOrWhiteSpace(fullyQualifiedName))
        {
            throw new ArgumentNullException(nameof(fullyQualifiedName));
        }

        // Create canonical string for hashing
        // Format: "FQN|params" where params can be empty
        var canonical = $"{fullyQualifiedName}|{parameterHash ?? string.Empty}";

        // Compute SHA256 hash
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));

        // Convert to lowercase hex string (using StringBuilder for better performance)
        var sb = new StringBuilder(hashBytes.Length * 2);
        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a stable hash from test parameters.
    /// </summary>
    /// <param name="parameters">The test parameters.</param>
    /// <returns>A canonical string representation of the parameters.</returns>
    /// <remarks>
    /// Parameter handling:
    /// - Null values: "null"
    /// - Numbers: Invariant culture formatting
    /// - Strings: Direct value
    /// - DateTime: ISO 8601 format (yyyy-MM-ddTHH:mm:ss.fffffffZ)
    /// - Arrays/Collections: Recursive formatting with square brackets
    /// - Objects: Type name + ToString()
    /// 
    /// Examples:
    /// - [2, 3, 5] → "2,3,5"
    /// - ["hello", null, 42] → "hello,null,42"
    /// - [new int[] {1, 2}, "test"] → "[1,2],test"
    /// </remarks>
    public static string GenerateParameterHash(object[] parameters)
    {
        if (parameters == null || parameters.Length == 0)
        {
            return string.Empty;
        }

        var paramStrings = parameters.Select(FormatParameter).ToArray();
        return string.Join(",", paramStrings);
    }

    /// <summary>
    /// Parses a fully qualified name into its components.
    /// </summary>
    /// <param name="fullyQualifiedName">The fully qualified name to parse.</param>
    /// <returns>A tuple containing namespace, class name, and method name.</returns>
    /// <exception cref="ArgumentException">Thrown when the name format is invalid.</exception>
    private static (string Namespace, string ClassName, string MethodName) ParseFullyQualifiedName(
        string fullyQualifiedName)
    {
        var parts = fullyQualifiedName.Split('.');

        if (parts.Length < 2)
        {
            throw new ArgumentException(
                $"Invalid fully qualified name format: '{fullyQualifiedName}'. " +
                "Expected format: 'Namespace.Class.Method' or 'Namespace.Nested.Class.Method'",
                nameof(fullyQualifiedName));
        }

        // Last part is method name
        var methodName = parts[parts.Length - 1];

        // Second to last is class name
        var className = parts[parts.Length - 2];

        // Everything before class is namespace
        var namespaceParts = parts.Take(parts.Length - 2);
        var namespaceName = string.Join(".", namespaceParts);

        return (namespaceName, className, methodName);
    }

    /// <summary>
    /// Formats a single parameter value to a stable string representation.
    /// </summary>
    private static string FormatParameter(object? parameter)
    {
        return parameter switch
        {
            null => "null",
            string str => str,
            bool b => b ? "true" : "false",
            byte b => b.ToString(CultureInfo.InvariantCulture),
            sbyte sb => sb.ToString(CultureInfo.InvariantCulture),
            short s => s.ToString(CultureInfo.InvariantCulture),
            ushort us => us.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            uint ui => ui.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            ulong ul => ul.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString("G9", CultureInfo.InvariantCulture),
            double d => d.ToString("G17", CultureInfo.InvariantCulture),
            decimal dec => dec.ToString(CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture),
            Guid g => g.ToString("D"),
            System.Collections.IEnumerable enumerable => FormatEnumerable(enumerable),
            _ => $"{parameter.GetType().Name}:{parameter}"
        };
    }

    /// <summary>
    /// Formats an enumerable parameter (arrays, lists, etc.) to a stable string.
    /// </summary>
    private static string FormatEnumerable(System.Collections.IEnumerable enumerable)
    {
        var items = new List<string>();

        foreach (var item in enumerable)
        {
            items.Add(FormatParameter(item));
        }

        return $"[{string.Join(",", items)}]";
    }

    /// <summary>
    /// Generates a default display name from method name and parameters.
    /// </summary>
    private static string GenerateDefaultDisplayName(string methodName, object[]? parameters)
    {
        if (parameters == null || parameters.Length == 0)
        {
            return methodName;
        }

        var paramHash = GenerateParameterHash(parameters);
        return $"{methodName}({paramHash})";
    }

    /// <summary>
    /// Generates a stable SHA256 hash for an error message to enable grouping of similar failures.
    /// </summary>
    /// <param name="errorMessage">The error message to hash.</param>
    /// <returns>A 64-character lowercase hex string (SHA256 hash), or null if input is null/empty.</returns>
    /// <remarks>
    /// This method creates a stable hash that can be used to group test failures with
    /// identical error messages, enabling analysis of failure patterns across test runs.
    /// </remarks>
    public static string? GenerateErrorMessageHash(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return null;
        }

        // Non-null after the check above
        return ComputeSha256Hash(errorMessage!);
    }

    /// <summary>
    /// Generates a stable SHA256 hash for a stack trace to enable grouping of similar failures.
    /// </summary>
    /// <param name="stackTrace">The stack trace to hash.</param>
    /// <returns>A 64-character lowercase hex string (SHA256 hash), or null if input is null/empty.</returns>
    /// <remarks>
    /// This method creates a stable hash that can be used to group test failures with
    /// identical stack traces, enabling analysis of failure locations and patterns in the codebase.
    /// </remarks>
    public static string? GenerateStackTraceHash(string? stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
        {
            return null;
        }

        // Non-null after the check above
        return ComputeSha256Hash(stackTrace!);
    }

    /// <summary>
    /// Computes a SHA256 hash for the given input string.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>A 64-character lowercase hex string (SHA256 hash).</returns>
    private static string ComputeSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));

        // Convert to lowercase hex string (using StringBuilder for better performance)
        var sb = new StringBuilder(hashBytes.Length * 2);
        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }
}
