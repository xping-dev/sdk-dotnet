/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Services.Identity;

/// <summary>
/// Generates stable, deterministic test identities for tracking tests across runs,
/// environments, and code changes.
/// </summary>
/// <remarks>
/// This generator creates consistent TestFingerprint hashes that enable:
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
public interface ITestIdentityGenerator
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
    /// <returns>A complete TestIdentity with stable TestFingerprint hash.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fullyQualifiedName or assembly is null.</exception>
    /// <exception cref="ArgumentException">Thrown when fullyQualifiedName is an invalid format.</exception>
    TestIdentity Generate(
        string fullyQualifiedName,
        string assembly,
        object[]? parameters = null,
        string? displayName = null,
        string? sourceFile = null,
        int? sourceLineNumber = null);

    /// <summary>
    /// Generates a stable, deterministic test identifier from a fully qualified test name
    /// and an optional parameter hash.
    /// </summary>
    /// <param name="fullyQualifiedName">The fully qualified test name (Namespace.Class.Method).</param>
    /// <param name="parameterHash">
    /// Optional hash of test parameters, as returned by <see cref="GenerateParameterHash"/>.
    /// When provided, the resulting ID distinguishes parameterized test variations.
    /// </param>
    /// <returns>A stable SHA256-based identifier string for the test.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fullyQualifiedName"/> is null.</exception>
    string GenerateTestId(string fullyQualifiedName, string? parameterHash = null);

    /// <summary>
    /// Generates a deterministic hash representing a set of test parameters.
    /// Used to distinguish variations of the same parameterized test.
    /// </summary>
    /// <param name="parameters">The parameter values passed to the test.</param>
    /// <returns>A stable hash string that uniquely identifies this combination of parameter values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameters"/> is null.</exception>
    string GenerateParameterHash(object[] parameters);

    /// <summary>
    /// Generates a deterministic hash of a test failure error message, enabling grouping of
    /// tests that fail with the same error across runs without storing the full message.
    /// </summary>
    /// <param name="errorMessage">The error message to hash, or <see langword="null"/> for passing tests.</param>
    /// <returns>
    /// A stable hash string if <paramref name="errorMessage"/> is non-null and non-empty;
    /// otherwise <see langword="null"/>.
    /// </returns>
    string? GenerateErrorMessageHash(string? errorMessage);

    /// <summary>
    /// Generates a deterministic hash of a stack trace, enabling deduplication of failures
    /// originating from the same code path across runs without storing the full trace.
    /// </summary>
    /// <param name="stackTrace">The stack trace to hash, or <see langword="null"/> if unavailable.</param>
    /// <returns>
    /// A stable hash string if <paramref name="stackTrace"/> is non-null and non-empty;
    /// otherwise <see langword="null"/>.
    /// </returns>
    string? GenerateStackTraceHash(string? stackTrace);
}
