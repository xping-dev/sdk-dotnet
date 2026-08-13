/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit.Retry;

/// <summary>
/// Marks a test case whose executions are recorded by Xping from inside the retry loop.
/// </summary>
/// <remarks>
/// Messages carrying a test case with this marker have already been recorded per attempt by
/// <see cref="XpingAttemptMessageBus"/>, so <see cref="XpingMessageSink"/> forwards them to the runner
/// without recording them a second time. Without this, the retry library's flushed final message —
/// which carries the cumulative duration of every attempt — would be recorded on top of the attempt
/// Xping already captured with its true duration.
/// </remarks>
internal interface IXpingManagedTestCase
{
}
