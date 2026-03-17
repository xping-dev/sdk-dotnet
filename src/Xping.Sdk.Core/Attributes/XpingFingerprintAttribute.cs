/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.RegularExpressions;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Attributes;

/// <summary>
/// Pins a constant fingerprint string to a test method, preventing the computed SHA256
/// from changing when the method, class, or namespace is renamed during refactoring.
/// </summary>
/// <remarks>
/// <para>
/// By default, Xping computes a <c>TestFingerprint</c> as a SHA256 hash of the test's
/// fully qualified name and parameter hash. That fingerprint changes whenever the method,
/// class, or namespace is renamed, breaking historical trend data on the Xping platform.
/// </para>
/// <para>
/// Applying this attribute pins a developer-chosen constant. The constant survives any
/// refactoring because it is embedded in source, not derived from names.
/// </para>
/// <para>
/// <strong>Priority rule:</strong> if this attribute is present on the test method, its
/// <see cref="Fingerprint"/> value is used as-is and the SHA256 computation is skipped.
/// When the attribute is absent, the SHA256 algorithm runs as normal.
/// </para>
/// <para>
/// <strong>Format constraint:</strong> the fingerprint must be a URL-safe slug containing
/// only alphanumeric characters, hyphens (<c>-</c>), or underscores (<c>_</c>),
/// and must not exceed 100 characters.
/// For example: <c>"login-happy-path-v1"</c> or <c>"checkout_smoke_42"</c>.
/// </para>
/// <para>
/// <strong>Parameterized tests:</strong> when applied to a parameterized test method
/// (e.g., NUnit <c>[TestCase]</c>, xUnit <c>[Theory]</c>, MSTest <c>[DataRow]</c>),
/// each parameter variant receives a unique fingerprint automatically derived from the
/// pinned value and the parameter values:
/// <c>"{Fingerprint}:{parameterHash}"</c>.
/// For example, <c>"login-v1:admin,true"</c> and <c>"login-v1:user,false"</c>.
/// The colon separator is reserved and not permitted in the pinned value, which is why
/// the format constraint excludes it.
/// </para>
/// </remarks>
/// <example>
/// Pin a fingerprint before renaming the test method:
/// <code>
/// [XpingFingerprint("checkout-happy-path-v1")]
/// [Test]
/// public void PlaceOrder_WithValidCart_Succeeds() { ... }
/// </code>
/// Parameterized — each variant gets a unique fingerprint automatically:
/// <code>
/// [XpingFingerprint("login-v1")]
/// [TestCase("admin", true)]
/// [TestCase("user", false)]
/// public void Login_ShouldSucceed(string role, bool expected) { ... }
/// // Produces: "login-v1:admin,true"  and  "login-v1:user,false"
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Method,
    AllowMultiple = false,
    Inherited = false)]
public sealed class XpingFingerprintAttribute : Attribute
{
    private const int MaxLength = 100;
    private static readonly Regex _validPattern =
        new(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

    /// <summary>
    /// Gets the constant fingerprint string that will be used as the test's stable identity.
    /// </summary>
    public string Fingerprint { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="XpingFingerprintAttribute"/>.
    /// </summary>
    /// <param name="fingerprint">
    /// A URL-safe slug that uniquely and stably identifies this test. Must contain only
    /// alphanumeric characters, hyphens (<c>-</c>), or underscores (<c>_</c>), and must
    /// not exceed 100 characters.
    /// Choose a value that is meaningful to your team and that you will not change
    /// once tests have been run (e.g., <c>"login-happy-path-v1"</c>).
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="fingerprint"/> is null, empty, whitespace, exceeds 100
    /// characters, or contains characters outside <c>[a-zA-Z0-9_-]</c>.
    /// </exception>
    public XpingFingerprintAttribute(string fingerprint)
    {
        Fingerprint = fingerprint.RequireNotNullOrWhiteSpace();

        if (Fingerprint.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Fingerprint must not exceed {MaxLength} characters. " +
                $"Supplied value has {Fingerprint.Length} characters.",
                nameof(fingerprint));
        }

        if (!_validPattern.IsMatch(Fingerprint))
        {
            throw new ArgumentException(
                "Fingerprint must only contain alphanumeric characters, hyphens (-), or underscores (_). " +
                $"Invalid value: \"{fingerprint}\".",
                nameof(fingerprint));
        }
    }
}
