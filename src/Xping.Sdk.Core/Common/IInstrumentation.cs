namespace Xping.Sdk.Core.Common;

/// <summary>
/// Represents an instrumentation interface for measuring execution time and tracking elapsed time.
/// </summary>
public interface IInstrumentation
{
    /// <summary>
    /// Gets the total elapsed time in milliseconds since the instrumentation started.
    /// </summary>
    long ElapsedMilliseconds { get; }

    /// <summary>
    /// Gets the total elapsed ticks (processor-specific units) since the instrumentation started.
    /// </summary>
    long ElapsedTicks { get; }

    /// <summary>
    /// Gets the total elapsed time as a <see cref="TimeSpan"/> since the instrumentation started.
    /// </summary>
    TimeSpan ElapsedTime { get; }

    /// <summary>
    /// Gets the timestamp when the instrumentation started.
    /// </summary>
    DateTime StartTime { get; }

    /// <summary>
    /// Gets a value indicating whether the instrumentation timer is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Restarts the instrumentation timer, resetting the elapsed time to zero. Use this method to measure a new 
    /// interval independently.
    /// </summary>
    void Restart();
}
