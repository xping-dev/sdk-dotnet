/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core.Common;

namespace Xping.Sdk.Common.UnitTests;

public sealed class InstrumentationLogTests
{
    [Test]
    public void StartDateIsSetToTodayWhenNewlyInstantiatedAndStartStopWatchIsDisabled()
    {
        // Arrange
        using var log = new InstrumentationTimer(startStopwatch: false);

        // Assert
        Assert.That(log.StartTime, Is.EqualTo(DateTime.Today));
    }

    [Test]
    public void StartDateIsNotSetToTodayWhenNewlyInstantiatedAndStartStopWatchIsEnabled()
    {
        // Arrange
        using var log = new InstrumentationTimer(startStopwatch: true);

        // Assert
        Assert.That(log.StartTime, Is.Not.EqualTo(DateTime.Today));
    }

    [Test]
    public void ElapsedTimeIsZeroWhenNewlyInstantiatedAndStartStopWatchIsDisabled()
    {
        // Arrange
        using var log = new InstrumentationTimer(startStopwatch: false);

        // Assert
        Assert.That(log.ElapsedTime, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void ElapsedMillisecondsIsZeroWhenNewlyInstantiatedAndStartStopWatchIsDisabled()
    {
        // Arrange
        using var log = new InstrumentationTimer(startStopwatch: false);

        // Assert
        Assert.That(log.ElapsedMilliseconds, Is.EqualTo(0));
    }

    [Test]
    public void ElapsedTicksIsZeroWhenNewlyInstantiatedAndStartStopWatchIsDisabled()
    {
        // Arrange
        using var log = new InstrumentationTimer(startStopwatch: false);

        // Assert
        Assert.That(log.ElapsedTicks, Is.EqualTo(0));
    }

    [Test]
    public void ElapsedTimeIsNotZeroWhenNewlyInstantiatedAndStartStopWatchIsEnabled()
    {
        // Arrange
        using var log = new InstrumentationTimer(startStopwatch: true);

        // Assert
        Assert.That(log.ElapsedTime, Is.Not.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void ElapsedMillisecondsIsNotZeroWhenNewlyInstantiatedAndStartStopWatchIsEnabled()
    {
        // Arrange
        using var log = new InstrumentationTimer(startStopwatch: true);

        // Act
        Thread.Sleep(100); // Give some time for the stopwatch to measure it.

        // Assert
        Assert.That(log.ElapsedMilliseconds, Is.Not.EqualTo(0));
    }

    [Test]
    public void ElapsedTicksIsNotZeroWhenNewlyInstantiatedAndStartStopWatchIsEnabled()
    {
        // Arrange
        using var log = new InstrumentationTimer(startStopwatch: true);

        // Assert
        Assert.That(log.ElapsedTicks, Is.Not.EqualTo(0));
    }

    [Test]
    public void StartTimeChangesAfterRestart()
    {
        // Arrange
        using var log = new InstrumentationTimer(startStopwatch: true);
        DateTime initialStartTime = log.StartTime;

        // Act
        Thread.Sleep(100); // Give some time for the stopwatch to measure it.
        log.Restart();

        // Assert
        Assert.That(log.StartTime, Is.GreaterThan(initialStartTime));
    }

    [Test]
    public void CallbackInvokedWhenInstrumentationLogIsDisposing()
    {
        // Arrange
        bool isInvoked = false;
        using (var log = new InstrumentationTimer(callback: log => isInvoked = true))
        { }

        // Assert
        Assert.That(isInvoked, Is.True);
    }

    [Test]
    public void StopwatchIsNotRunningWhenInstrumentationLogIsDisposingAndCallbackInvoked()
    {
        // Arrange
        bool isRunning = true;
        using (var log = new InstrumentationTimer(callback: log => isRunning = log.IsRunning))
        { }

        // Assert
        Assert.That(isRunning, Is.False);
    }

    [Test]
    public void StopwatchIsRunningWhenNotDisposed()
    {
        // Arrange
        using var log = new InstrumentationTimer();

        // Assert
        Assert.That(log.IsRunning, Is.True);
    }
}
