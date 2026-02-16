/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Services.Identity.Internals;

/// <inheritdoc/>
internal sealed class TestIdentityGenerator : ITestIdentityGenerator
{
    private readonly TestIdentityBuilder _builder = new();

    /// <inheritdoc/>
    TestIdentity ITestIdentityGenerator.Generate(
        string fullyQualifiedName,
        string assembly,
        object[]? parameters,
        string? displayName,
        string? sourceFile,
        int? sourceLineNumber)
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
        (string @namespace, string className, string methodName) = ParseFullyQualifiedName(fullyQualifiedName);

        // Generate parameter hash if parameters exist
        string? parameterHash = parameters is { Length: > 0 }
            ? GenerateParameterHash(parameters)
            : null;

        // Generate stable test ID
        string testId = GenerateTestId(fullyQualifiedName, parameterHash);

        TestIdentity testIdentity = _builder.Reset()
            .WithTestId(testId)
            .WithFullyQualifiedName(fullyQualifiedName)
            .WithAssembly(assembly)
            .WithNamespace(@namespace)
            .WithClassName(className)
            .WithMethodName(methodName)
            .WithDisplayName(displayName ?? GenerateDefaultDisplayName(methodName, parameters))
            .WithParameterHash(parameterHash)
            .WithSourceFile(sourceFile)
            .WithSourceLineNumber(sourceLineNumber)
            .Build();

        return testIdentity;
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
    public string GenerateTestId(string fullyQualifiedName, string? parameterHash = null)
    {
        if (string.IsNullOrWhiteSpace(fullyQualifiedName))
        {
            throw new ArgumentNullException(nameof(fullyQualifiedName));
        }

        // Create a canonical string for hashing
        // Format: "FQN|params" where params can be empty
        string canonical = $"{fullyQualifiedName}|{parameterHash ?? string.Empty}";

        // Compute SHA256 hash
        using SHA256? sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));

        // Convert to lowercase hex string (using StringBuilder for better performance)
        StringBuilder sb = new(hashBytes.Length * 2);
        foreach (byte b in hashBytes)
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
    public string GenerateParameterHash(object[] parameters)
    {
        if (parameters.Length == 0)
        {
            return string.Empty;
        }

        string[] paramStrings = parameters.Select(FormatParameter).ToArray();
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
        string[] parts = fullyQualifiedName.Split('.');

        if (parts.Length < 2)
        {
            throw new ArgumentException(
                $"Invalid fully qualified name format: '{fullyQualifiedName}'. " +
                "Expected format: 'Namespace.Class.Method' or 'Namespace.Nested.Class.Method'",
                nameof(fullyQualifiedName));
        }

        // The last part is the method name
        string methodName = parts[parts.Length - 1];

        // Second to last is the class name
        string className = parts[parts.Length - 2];

        // Everything before class is a namespace
        IEnumerable<string> namespaceParts = parts.Take(parts.Length - 2);
        string namespaceName = string.Join(".", namespaceParts);

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
        List<string> items = [];

        foreach (object? item in enumerable)
        {
            items.Add(FormatParameter(item));
        }

        return $"[{string.Join(",", items)}]";
    }

    /// <summary>
    /// Generates a default display name from method name and parameters.
    /// </summary>
    private string GenerateDefaultDisplayName(string methodName, object[]? parameters)
    {
        if (parameters == null || parameters.Length == 0)
        {
            return methodName;
        }

        string paramHash = GenerateParameterHash(parameters);
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
    public string? GenerateErrorMessageHash(string? errorMessage)
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
    public string? GenerateStackTraceHash(string? stackTrace)
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
        using SHA256? sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));

        // Convert to lowercase hex string (using StringBuilder for better performance)
        StringBuilder sb = new(hashBytes.Length * 2);
        foreach (byte b in hashBytes)
        {
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }
}
