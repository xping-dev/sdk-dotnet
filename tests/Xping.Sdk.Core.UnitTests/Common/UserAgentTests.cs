using Xping.Sdk.Core.Common;

namespace Xping.Sdk.Core.UnitTests.Common;

public sealed class UserAgentTests
{
    [Test]
    public void ChromeDesktopIsNotEmpty()
    {
        Assert.That(UserAgent.ChromeDesktop, Is.Not.Empty);
    }

    [Test]
    public void ChromeDesktopContainsChromeName()
    {
        Assert.That(UserAgent.ChromeDesktop, Does.Contain("Chrome"));
    }

    [Test]
    public void FirefoxDesktopIsNotEmpty()
    {
        Assert.That(UserAgent.FirefoxDesktop, Is.Not.Empty);
    }

    [Test]
    public void FirefoxDesktopContainsChromeName()
    {
        Assert.That(UserAgent.FirefoxDesktop, Does.Contain("Firefox"));
    }

    [Test]
    public void EdgeDesktopIsNotEmpty()
    {
        Assert.That(UserAgent.EdgeDesktop, Is.Not.Empty);
    }

    [Test]
    public void EdgeDesktopContainsChromeName()
    {
        Assert.That(UserAgent.EdgeDesktop, Does.Contain("Edge"));
    }

    [Test]
    public void SafariMobileIsNotEmpty()
    {
        Assert.That(UserAgent.SafariMobile, Is.Not.Empty);
    }

    [Test]
    public void SafariMobileContainsChromeName()
    {
        Assert.That(UserAgent.SafariMobile, Does.Contain("Safari"));
    }

    [Test]
    public void ChromeiOSMobileIsNotEmpty()
    {
        Assert.That(UserAgent.ChromeiOSMobile, Is.Not.Empty);
    }

    [Test]
    public void ChromeiOSMobileContainsChromeName()
    {
        Assert.That(UserAgent.ChromeiOSMobile, Does.Contain("Safari"));
    }

    [Test]
    public void ChromeAndroidMobileIsNotEmpty()
    {
        Assert.That(UserAgent.ChromeAndroidMobile, Is.Not.Empty);
    }

    [Test]
    public void ChromeAndroidMobileContainsChromeName()
    {
        Assert.That(UserAgent.ChromeAndroidMobile, Does.Contain("Chrome"));
    }

    [Test]
    public void FirefoxiOSMobileIsNotEmpty()
    {
        Assert.That(UserAgent.FirefoxiOSMobile, Is.Not.Empty);
    }

    [Test]
    public void FirefoxiOSMobileContainsChromeName()
    {
        Assert.That(UserAgent.FirefoxiOSMobile, Does.Contain("Safari"));
    }

    [Test]
    public void FirefoxAndroidMobileIsNotEmpty()
    {
        Assert.That(UserAgent.FirefoxAndroidMobile, Is.Not.Empty);
    }

    [Test]
    public void FirefoxAndroidMobileContainsChromeName()
    {
        Assert.That(UserAgent.FirefoxAndroidMobile, Does.Contain("Firefox"));
    }
}
