using System.Net;
using Xping.Sdk.Core.Clients.Http;

namespace Xping.Sdk.Core.UnitTests.HttpClients.Configurations;

public sealed class HttpClientFactoryConfigurationTests
{
    [Test]
    public void PooledConnectionLifetimeShouldHaveOneMinuteByDefault()
    {
        // Arrange
        var httpClientConfiguration = new HttpClientFactoryConfiguration();

        // Assert
        Assert.That(httpClientConfiguration.PooledConnectionLifetime, Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void SleepDurationsShouldHaveCorrectDefaultValues()
    {
        // Arrange
        var httpClientConfiguration = new HttpClientFactoryConfiguration();

        // Act
        var sleepDurations = httpClientConfiguration.SleepDurations.ToList();

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(sleepDurations[0], Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(sleepDurations[1], Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(sleepDurations[2], Is.EqualTo(TimeSpan.FromSeconds(10)));
        });
    }

    [Test]
    public void HandledEventsAllowedBeforeBreakingShouldReturnThreeByDefault()
    {
        // Arrange
        var httpClientConfiguration = new HttpClientFactoryConfiguration();

        // Assert
        Assert.That(httpClientConfiguration.HandledEventsAllowedBeforeBreaking, Is.EqualTo(3));
    }

    [Test]
    public void DurationOfBreakShouldBe30SecondsByDefault()
    {
        // Arrange
        var httpClientConfiguration = new HttpClientFactoryConfiguration();

        // Act
        Assert.That(httpClientConfiguration.DurationOfBreak, Is.EqualTo(TimeSpan.FromSeconds(30)));
    }

    [Test]
    public void AutomaticDecompressionShouldBeEnabledForAllByDefault()
    {
        // Arrange
        var httpClientConfiguration = new HttpClientFactoryConfiguration();

        // Act
        Assert.That(httpClientConfiguration.AutomaticDecompression, Is.EqualTo(DecompressionMethods.All));
    }
}
