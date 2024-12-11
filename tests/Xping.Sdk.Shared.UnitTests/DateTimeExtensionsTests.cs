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
        Assert.That(time, Is.EqualTo("100 ms"));
    }

    [Test]
    public void GetFormattedTimeReturnsSecondsWhenLessThanAMinute()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromSeconds(10);

        // Act
        string time = span.GetFormattedTime();

        // Assert
        Assert.That(time, Is.EqualTo("10 s"));
    }

    [Test]
    public void GetFormattedTimeReturnsMinutesWhenEqualToOneMinute()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMinutes(1);

        // Act
        string time = span.GetFormattedTime();

        // Assert
        Assert.That(time, Is.EqualTo("1 min"));
    }

    [Test]
    public void GetFormattedTimeReturnsMinutesWhenGreaterThanOneMinute()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMinutes(5);

        // Act
        string time = span.GetFormattedTime();

        // Assert
        Assert.That(time, Is.EqualTo("5 min"));
    }

    [Test]
    public void GetFormattedTimeRoundsMillisecondsToNearestWholeNumber()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMilliseconds(0.6);

        // Act
        string time = span.GetFormattedTime();

        // Assert
        Assert.That(time, Is.EqualTo("1 ms"));
    }

    [Test]
    public void GetFormattedTimeRoundsMillisecondsToZeroDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMilliseconds(0.4);

        // Act
        string time = span.GetFormattedTime();

        // Assert
        Assert.That(time, Is.EqualTo("0 ms"));
    }

    [Test]
    public void GetFormattedTimeRoundsSecondsToTwoDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromSeconds(4.469);

        // Act
        string time = span.GetFormattedTime();

        // Assert
        Assert.That(time, Is.EqualTo("4.47 s"));
    }

    [Test]
    public void GetFormattedTimeRoundsMinutesToTwoDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMinutes(4.469);

        // Act
        string time = span.GetFormattedTime();

        // Assert
        Assert.That(time, Is.EqualTo("4.47 min"));
    }

    [Test]
    public void GetFormattedTimeInMillisecondsShouldBeRoundedToZeroDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromSeconds(4.4693);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Millisecond);

        // Assert
        Assert.That(time, Is.EqualTo("4469 ms"));
    }

    [Test]
    public void GetFormattedTimeInSecondsShouldBeRoundedToTwoDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMilliseconds(4469);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Second);

        // Assert
        Assert.That(time, Is.EqualTo("4.47 s"));
    }

    [Test]
    public void GetFormattedTimeInMinutesShouldBeRoundedToTwoDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromSeconds(122.4321);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Minute);

        // Assert
        Assert.That(time, Is.EqualTo("2.04 min"));
    }

    [Test]
    public void GetFormattedTimeInHoursShouldBeRoundedToTwoDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMinutes(122.4321);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Hour);

        // Assert
        Assert.That(time, Is.EqualTo("2.04 hours"));
    }

    [Test]
    public void GetFormattedTimeInHoursShouldUseSingularUnitForValueEqualToOneHour()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromHours(1.5);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Hour);

        // Assert
        Assert.That(time, Is.EqualTo("1.5 hour"));
    }

    [Test]
    public void GetFormattedTimeInHoursShouldRetunZeroHoursForSmallRoundedValues()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMilliseconds(1);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Hour);

        // Assert
        Assert.That(time, Is.EqualTo("0 hours"));
    }

    [Test]
    public void GetFormattedTimeInDaysShouldBeRoundedToTwoDecimalPlaces()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromHours(49.4321);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Day);

        // Assert
        Assert.That(time, Is.EqualTo("2.06 days"));
    }

    [Test]
    public void GetFormattedTimeInDaysShouldUseSingularUnitForValueEqualToOneDay()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromDays(1.5);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Day);

        // Assert
        Assert.That(time, Is.EqualTo("1.5 day"));
    }

    [Test]
    public void GetFormattedTimeInDaysShouldRetunZeroDaysForSmallRoundedValues()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMilliseconds(1);

        // Act
        string time = span.GetFormattedTime(TimeSpanUnit.Day);

        // Assert
        Assert.That(time, Is.EqualTo("0 days"));
    }

    [Test]
    public void GetFormattedTimeWhenUnitAreNotRecognized()
    {
        // Arrange
        TimeSpan span = TimeSpan.FromMinutes(1.5);

        // Act
        string time = span.GetFormattedTime((TimeSpanUnit)19);

        // Assert
        Assert.That(time, Is.EqualTo("1.5 min"));
    }
}
