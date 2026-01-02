/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Upload;

using System;
using System.Collections.Generic;
using System.Linq;
using Xping.Sdk.Core.Models;

/// <summary>
/// Optimizes batches of test executions by deduplicating session context.
/// Preserves session context only at session boundaries while maintaining all unique sessions,
/// reducing payload size significantly for large test runs with one or multiple test sessions.
/// </summary>
public static class TestExecutionBatchOptimizer
{
    /// <summary>
    /// Optimizes a batch of test executions by deduplicating session context across consecutive executions.
    /// </summary>
    /// <param name="executions">The test executions to optimize.</param>
    /// <returns>Optimized list where only session boundary executions contain session context.</returns>
    /// <remarks>
    /// This method intelligently handles multiple test sessions within a batch. It preserves
    /// session context only at session boundaries (first occurrence of each unique session),
    /// while removing duplicate session references for subsequent executions in the same session.
    /// This significantly reduces payload size while maintaining all session information.
    /// 
    /// Example: If executions have sessions [A,A,A,B,B,C,C,C], the optimized result keeps
    /// sessions at positions [0,3,5] and nulls the rest, preserving all three unique sessions.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when executions is null.</exception>
    public static IReadOnlyList<TestExecution> OptimizeForTransport(IEnumerable<TestExecution> executions)
    {
        if (executions == null)
        {
            throw new ArgumentNullException(nameof(executions));
        }

        var executionList = executions.ToList();

        if (executionList.Count == 0)
        {
            return executionList;
        }

        // Track the current session to detect boundaries
        string? currentSessionId = null;

        for (int i = 0; i < executionList.Count; i++)
        {
            var execution = executionList[i];
            var sessionContext = execution.SessionContext;

            if (sessionContext == null)
            {
                // Execution has no session, keep as-is
                continue;
            }

            var sessionId = sessionContext.SessionId;

            if (currentSessionId == null || currentSessionId != sessionId)
            {
                // This is a session boundary - keep the session context
                currentSessionId = sessionId;
            }
            else
            {
                // Same session as previous - remove duplicate session context
                executionList[i].SessionContext = null;
            }
        }

        return executionList;
    }

    /// <summary>
    /// Rehydrates session context for all executions in a batch after receiving from transport.
    /// </summary>
    /// <param name="executions">The test executions received from transport.</param>
    /// <returns>List with session context populated for all executions.</returns>
    /// <remarks>
    /// This method restores session context for all executions by propagating sessions forward
    /// from session boundaries. When it encounters an execution with a session context, that
    /// session becomes the "current" session and is applied to all subsequent executions
    /// until a new session boundary is encountered.
    /// 
    /// This correctly handles multiple sessions in a batch, ensuring each execution receives
    /// the appropriate session context based on the session boundaries preserved during optimization.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when executions is null.</exception>
    public static IReadOnlyList<TestExecution> RehydrateFromTransport(IEnumerable<TestExecution> executions)
    {
        if (executions == null)
        {
            throw new ArgumentNullException(nameof(executions));
        }

        var executionList = executions.ToList();

        if (executionList.Count == 0)
        {
            return executionList;
        }

        // Track current session and apply it forward until we hit a new session boundary
        TestSession? currentSession = null;

        for (int i = 0; i < executionList.Count; i++)
        {
            var execution = executionList[i];

            if (execution.SessionContext != null)
            {
                // This is a session boundary - update current session
                currentSession = execution.SessionContext;
            }
            else if (currentSession != null)
            {
                // Apply current session to executions without context
                executionList[i].SessionContext = currentSession;
            }
            // If both are null, leave the execution as-is (shouldn't happen in normal flow)
        }

        return executionList;
    }
}
