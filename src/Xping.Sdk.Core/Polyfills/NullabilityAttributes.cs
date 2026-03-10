/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

#if !NETCOREAPP3_0_OR_GREATER && !NETSTANDARD2_1_OR_GREATER

namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Polyfill that enables <c>[NotNullWhen(true/false)]</c> on netstandard2.0 targets.
/// The C# nullable-analysis compiler honours this attribute at compile time;
/// no runtime behaviour is associated with it.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
internal sealed class NotNullWhenAttribute(bool returnValue) : Attribute
{
    /// <summary>
    /// Gets the return value condition under which the annotated parameter is guaranteed non-null.
    /// </summary>
    public bool ReturnValue { get; } = returnValue;
}

#endif
