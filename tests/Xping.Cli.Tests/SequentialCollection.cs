/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Tests;

/// <summary>
/// Serializes test classes that mutate process-wide state such as environment variables.
/// All classes decorated with <c>[Collection("Sequential")]</c> run one at a time.
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public sealed class SequentialCollection { }
