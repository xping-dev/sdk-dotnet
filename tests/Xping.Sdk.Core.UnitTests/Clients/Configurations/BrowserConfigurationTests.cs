using Microsoft.Playwright;
using Xping.Sdk.Core.Clients.Browser;
using Xping.Sdk.Core.Common;

namespace Xping.Sdk.Core.UnitTests.HttpClients.Configurations;

public sealed class BrowserConfigurationTests
{
    [Test]
    public void SetGeolocationStoresCoordinates()
    {
        // Arrange
        var geolocation = new Geolocation()
        {
            Latitude = 45,
            Longitude = 90,
            Accuracy = 100
        };
        var browserConfiguration = new BrowserConfiguration();

        // Act
        browserConfiguration.SetGeolocation(geolocation);

        // Assert
        Assert.That(
            browserConfiguration.PropertyBag.GetProperty<Geolocation>(PropertyBagKeys.Geolocation),
            Is.EqualTo(geolocation));
    }

    [Test]
    public void GetGeolocationRetrievesCoordinates()
    {
        // Arrange
        var geolocation = new Geolocation()
        {
            Latitude = 45,
            Longitude = 90,
            Accuracy = 100
        };
        var browserConfiguration = new BrowserConfiguration();
        browserConfiguration.SetGeolocation(geolocation);

        // Act
        var coordinates = browserConfiguration.GetGeolocation();

        // Assert
        Assert.That(coordinates, Is.EqualTo(geolocation));
    }

    [Test]
    public void ClearGeolocationRemovesCoordinates()
    {
        // Arrange
        var geolocation = new Geolocation()
        {
            Latitude = 45,
            Longitude = 90,
            Accuracy = 100
        };
        var browserConfiguration = new BrowserConfiguration();
        browserConfiguration.SetGeolocation(geolocation);

        // Act
        browserConfiguration.ClearGeolocation();

        // Assert
        Assert.That(browserConfiguration.GetGeolocation(), Is.Null);
    }
}
