/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Orchestration;

/// <summary>
/// xUnit collection that disables parallelism for tests that mutate process-global state
/// (e.g. environment variables).  All test classes decorated with <c>[Collection("Sequential")]</c>
/// run serially with respect to each other, preventing races when env vars are set/cleared.
/// </summary>
// xUnit requires collection-definition classes to be public (xUnit1027); suppress the
// CA1515 analyzer rule that would otherwise request internal visibility.
#pragma warning disable CA1515
[CollectionDefinition("Sequential", DisableParallelization = true)]
public sealed class SequentialCollection { }
#pragma warning restore CA1515
