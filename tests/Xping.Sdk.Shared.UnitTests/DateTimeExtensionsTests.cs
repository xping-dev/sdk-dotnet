/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Globalization;

namespace Xping.Sdk.Shared.UnitTests;

public sealed class DateTimeExtensionsTests
{
    [Test]
    public void GetFormattedTimeReturnsMillisecondsWhenLessThanASecond()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMilliseconds(100);

        // Act
        string time = span.GetFormattedTime();

        // Assert
        AssertTimeEquals(time, 100, "ms");
    }

    [Test]
    public void GetFormattedTimeReturnsSecondsWhenLessThanAMinute()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromSeconds(10);

        // Act
        string time = span.GetFormattedTime();

        // Assert
        AssertTimeEquals(time, 10, "s");
    }

    [Test]
    public void GetFormattedTimeReturnsMinutesWhenEqualToOneMinute()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMinutes(1);

        // Act
        string time = span.GetFormattedTime();

        // Assert
        AssertTimeEquals(time, 1, "min");
    }

    [Test]
    public void GetFormattedTimeReturnsMinutesWhenGreaterThanOneMinute()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMinutes(5);

        // Act
        string time = span.GetFormattedTime();

        // Assert
        AssertTimeEquals(time, 5, "min");
    }

    [Test]
    public void GetFormattedTimeRoundsMillisecondsToNearestWholeNumber()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMilliseconds(0.6);

        // Act
        string time = span.GetFormattedTime();

        // Assert
        AssertTimeEquals(time, 1, "ms");
    }

    [Test]
    public void GetFormattedTimeRoundsMillisecondsToZeroDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMilliseconds(0.4);

        // Act
        string time = span.GetFormattedTime();

        // Assert
        AssertTimeEquals(time, 0, "ms");
    }

    [Test]
    public void GetFormattedTimeRoundsSecondsToTwoDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromSeconds(4.469);

        // Act
        string time = span.GetFormattedTime();

        // Assert
        AssertTimeEquals(time, 4.47, "s");
    }

    [Test]
    public void GetFormattedTimeRoundsMinutesToTwoDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMinutes(4.469);

        // Act
        string time = span.GetFormattedTime();

        // Assert
        AssertTimeEquals(time, 4.47, "min");
    }

    [Test]
    public void GetFormattedTimeInMillisecondsShouldBeRoundedToZeroDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromSeconds(4.4693);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Millisecond);

        // Assert
        AssertTimeEquals(time, 4469, "ms");
    }

    [Test]
    public void GetFormattedTimeInSecondsShouldBeRoundedToTwoDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMilliseconds(4469);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Second);

        // Assert
        AssertTimeEquals(time, 4.47, "s");
    }

    [Test]
    public void GetFormattedTimeInMinutesShouldBeRoundedToTwoDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromSeconds(122.4321);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Minute);

        // Assert
        AssertTimeEquals(time, 2.04, "min");
    }

    [Test]
    public void GetFormattedTimeInHoursShouldBeRoundedToTwoDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMinutes(122.4321);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Hour);

        // Assert
        AssertTimeEquals(time, 2.04, "hours");
    }

    [Test]
    public void GetFormattedTimeInHoursShouldUseSingularUnitForValueEqualToOneHour()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromHours(1.5);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Hour);

        // Assert
        AssertTimeEquals(time, 1.5, "hour");
    }

    [Test]
    public void GetFormattedTimeInHoursShouldRetunZeroHoursForSmallRoundedValues()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMilliseconds(1);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Hour);

        // Assert
        AssertTimeEquals(time, 0, "hours");
    }

    [Test]
    public void GetFormattedTimeInDaysShouldBeRoundedToTwoDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromHours(49.4321);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Day);

        // Assert
        AssertTimeEquals(time, 2.06, "days");
    }

    [Test]
    public void GetFormattedTimeInDaysShouldUseSingularUnitForValueEqualToOneDay()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromDays(1.5);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Day);

        // Assert
        AssertTimeEquals(time, 1.5, "day");
    }

    [Test]
    public void GetFormattedTimeInDaysShouldRetunZeroDaysForSmallRoundedValues()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMilliseconds(1);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Day);

        // Assert
        AssertTimeEquals(time, 0, "days");
    }

    [Test]
    public void GetFormattedTimeWhenUnitAreNotRecognized()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMinutes(1.5);

        // Act
        string time = span.GetFormattedTime((TimeSpanUnit)19);

        // Assert
        AssertTimeEquals(time, 1.5, "min");
    }
    
    private static void AssertTimeEquals(string actualTime, double expectedValue, string expectedUnit)
    {
        var parts = actualTime.Split(' ');
        Assert.That(parts, Has.Length.EqualTo(2), $"Time format should be 'number {expectedUnit}'");
        Assert.That(parts[1], Is.EqualTo(expectedUnit), $"Unit should be '{expectedUnit}'");
    
        // Handle both comma and dot as decimal separators
        var numericPart = parts[0].Replace(',', '.');
        var numericValue = double.Parse(numericPart, CultureInfo.InvariantCulture);
        
        Assert.That(numericValue, Is.EqualTo(expectedValue).Within(0.001));
    }
}
