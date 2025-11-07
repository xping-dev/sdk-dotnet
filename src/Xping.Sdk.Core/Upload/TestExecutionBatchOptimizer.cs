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
/// Only the first execution in a batch contains the full session context,
/// reducing payload size significantly for large test runs.
/// </summary>
public static class TestExecutionBatchOptimizer
{
    /// <summary>
    /// Optimizes a batch of test executions by extracting session context to the first execution only.
    /// </summary>
    /// <param name="executions">The test executions to optimize.</param>
    /// <returns>Optimized list where only first execution contains session context.</returns>
    /// <remarks>
    /// This method assumes all executions in the batch share the same session.
    /// The session context from the first execution is preserved, while all others are set to null.
    /// Server-side processing must rehydrate the context for all executions.
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

        // Ensure first execution has session context
        // Keep it as-is, remove from all others
        for (int i = 1; i < executionList.Count; i++)
        {
            executionList[i].SessionContext = null;
        }

        return executionList;
    }

    /// <summary>
    /// Rehydrates session context for all executions in a batch after receiving from server.
    /// </summary>
    /// <param name="executions">The test executions received from transport.</param>
    /// <returns>List with session context populated for all executions.</returns>
    /// <remarks>
    /// This method copies the session context from the first execution to all subsequent
    /// executions that don't have their own context. If the first execution doesn't have
    /// a session context, the executions are returned unchanged.
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

        // Get context from first execution
        var sessionContext = executionList[0].SessionContext;

        if (sessionContext == null)
        {
            // This shouldn't happen in normal flow, but handle gracefully
            return executionList;
        }

        // Apply to all executions that don't have context
        for (int i = 1; i < executionList.Count; i++)
        {
            if (executionList[i].SessionContext == null)
            {
                executionList[i].SessionContext = sessionContext;
            }
        }

        return executionList;
    }
}
