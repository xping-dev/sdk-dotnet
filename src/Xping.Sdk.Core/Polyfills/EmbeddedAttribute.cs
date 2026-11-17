/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Microsoft.CodeAnalysis;

/// <summary>
/// Hides the type it marks from every other assembly, including the ones named by
/// <c>InternalsVisibleTo</c>. The compiler recognises it by name.
/// </summary>
/// <remarks>
/// The CLI and the test projects see Core's internals and target net10.0, where the BCL already
/// defines the attributes the <c>Polyfills/</c> folder supplies for netstandard2.0. Without this,
/// both copies are visible to them and every use fails with CS0433.
/// </remarks>
[Embedded]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Interface | AttributeTargets.Delegate, Inherited = false)]
internal sealed class EmbeddedAttribute : Attribute;
