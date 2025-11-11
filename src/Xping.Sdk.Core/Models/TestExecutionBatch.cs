/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models;

using System.Collections.Generic;

/// <summary>
/// Represents a batch of test executions for serialization and deserialization.
/// This is the wire format used when transmitting test executions to/from the Xping API.
/// </summary>
/// <remarks>
/// <para>
/// This class is used both internally by the SDK and can be used by consumers who need
/// to serialize or deserialize batches of test executions using the Xping serialization format.
/// </para>
/// <para>
/// <strong>Usage Example - Serialization:</strong>
/// <code>
/// var serializer = new XpingJsonSerializer();
/// var batch = new TestExecutionBatch
/// {
///     Executions = new List&lt;TestExecution&gt; { execution1, execution2 }
/// };
/// string json = serializer.Serialize(batch);
/// </code>
/// </para>
/// <para>
/// <strong>Usage Example - Deserialization:</strong>
/// <code>
/// var serializer = new XpingJsonSerializer();
/// var batch = serializer.Deserialize&lt;TestExecutionBatch&gt;(json);
/// List&lt;TestExecution&gt; executions = batch?.Executions ?? new List&lt;TestExecution&gt;();
/// </code>
/// </para>
/// </remarks>
public sealed class TestExecutionBatch
{
    /// <summary>
    /// Gets or sets the collection of test executions in this batch.
    /// </summary>
#pragma warning disable CA2227 // Collection properties should be read only - Required for JSON deserialization
#pragma warning disable CA1002 // Do not expose generic lists - Wire format requires List for compatibility
    public List<TestExecution> Executions { get; set; } = new();
#pragma warning restore CA1002
#pragma warning restore CA2227
}
