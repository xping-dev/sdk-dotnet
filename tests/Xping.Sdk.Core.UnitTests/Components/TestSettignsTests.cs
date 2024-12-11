using Xping.Sdk.Core.Components;

namespace Xping.Sdk.UnitTests.Components;

public sealed class TestSettignsTests
{
    [Test]
    public void ClassTypeShouldBeSealed()
    {
        Type type = typeof(TestSettings);
        Assert.That(type.IsSealed, Is.True, $"The class '{nameof(TestSettings)}' should be sealed.");
    }

    [Test]
    public void ContinueOnFailureShouldBeDisabledByDefault()
    {
        // Arrange
        var settings = new TestSettings();

        // Assert
        Assert.That(settings.ContinueOnFailure, Is.False);
    }

    [Test]
    public void TimeoutShouldBeSetTo30SecondsInReleaseAnd30MinutesInDebugByDefault()
    {
        // Arrange
        var settings = new TestSettings();

        // Assert
#if DEBUG
        Assert.That(settings.Timeout, Is.EqualTo(TimeSpan.FromMinutes(30)));
#else
        Assert.That(settings.Timeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
#endif
    }
}
