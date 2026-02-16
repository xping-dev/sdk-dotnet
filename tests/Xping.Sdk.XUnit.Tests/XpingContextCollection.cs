/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;

namespace Xping.Sdk.XUnit.Tests;

/// <summary>
/// xUnit collection definition that serializes all test classes sharing the
/// static <see cref="XpingContext"/> singleton, preventing parallel execution.
/// </summary>
[SuppressMessage("Design", "CA1515", Justification = "xUnit requires collection definitions to be public (xUnit1027)")]
[CollectionDefinition("XpingContext")]
public sealed class XpingContextCollection { }
