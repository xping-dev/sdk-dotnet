/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Diagnostics;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Common;

/// <summary>
/// This class provides a set utilities that can be used to log data related to code execution time.
/// </summary>
/// <example>
/// <code>
/// using (var log = InstrumentationTimer())
/// {
///     // start of the code for which to measure execution time
///     // end of the code
///     Console.WriteLine($"Code started at {log.StartTime} and took {log.ElapsedTime.TotalMilliseconds} [ms] to run. 
/// }
/// </code>
/// </example>
public sealed class InstrumentationTimer : IInstrumentation, IDisposable
{
    private bool _isDisposed;
    private readonly ThreadLocal<Stopwatch> _threadLocalStopwatch;
    private readonly Stopwatch _stopwatch;
    private readonly Action<InstrumentationTimer>? _callback;
    private DateTime _startTime = DateTime.Today;

    /// <summary>
    /// Initializes a new instance of the InstrumentationTimer class.
    /// </summary>
    /// <param name="callback">An optional callback action that will be invoked when the instance is disposed.</param>
    /// <param name="startStopwatch">A flag indicating whether to start the stopwatch automatically.</param>
    public InstrumentationTimer(Action<InstrumentationTimer>? callback = null, bool startStopwatch = true)
    {
        _callback = callback;
        _threadLocalStopwatch = new(valueFactory: () => new Stopwatch());
        _stopwatch = _threadLocalStopwatch.Value.RequireNotNull(nameof(_threadLocalStopwatch.Value));

        if (startStopwatch)
        {
            Restart();
        }
    }

    /// <summary>
    /// Gets the total elapsed time measured by the current instaince, in milliseconds.
    /// </summary>
    public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

    /// <summary>
    /// Gets the total elapsed time measured by the current instance, in timer ticks.
    /// </summary>
    public long ElapsedTicks => _stopwatch.ElapsedTicks;

    /// <summary>
    /// Gets the total elapsed time measured by the current instance.
    /// </summary>
    public TimeSpan ElapsedTime => _stopwatch.Elapsed;

    /// <summary>
    /// Gets the time that the associated stopwatch was started.
    /// </summary>
    public DateTime StartTime => _startTime;

    /// <summary>
    /// Gets a value indicating whether the stopwatch timer is running.
    /// </summary>
    public bool IsRunning => _stopwatch.IsRunning;

    /// <summary>
    /// Stops time measurement, resets the elapsed time to zero and start time to current time and starts measuring.
    /// </summary>
    public void Restart()
    {
        _startTime = DateTime.UtcNow;
        _stopwatch.Restart();
    }

    /// <summary>
    /// Releases the resources used by the InstrumentationTimer instance.
    /// </summary>
    /// <remarks>
    /// This method stops the stopwatch and invokes the callback action if provided.
    /// </remarks>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            _stopwatch.Stop();
            _callback?.Invoke(this);
            _threadLocalStopwatch.Dispose();
        }

        _isDisposed = true;
    }
}
