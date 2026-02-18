/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

#if !NET5_0_OR_GREATER

namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill that enables the C# 9 'init' accessor on netstandard2.0 and .NET Framework targets.
/// The C# compiler emits a reference to this type for every 'init' setter; the CLR only requires
/// the type to be resolvable — it imposes no runtime behaviour of its own.
/// </summary>
internal static class IsExternalInit { }

#endif
