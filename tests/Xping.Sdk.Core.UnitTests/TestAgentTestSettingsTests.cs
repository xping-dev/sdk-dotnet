/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Xping.Sdk.Core;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.DependencyInjection;
using Xping.Sdk.Core.Session;
using Xping.Sdk.UnitTests.TestFixtures;

namespace Xping.Sdk.UnitTests;

/// <summary>
/// Tests for TestAgent TestSettings functionality including null handling and defaults.
/// </summary>
[SetUpFixture]
[TestFixtureSource(typeof(TestFixtureProvider), nameof(TestFixtureProvider.ServiceProvider))]
public sealed class TestAgentTestSettingsTests(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    [Test]
    public async Task TestAgentWithNullTestSettingsUsesDefaults()
    {
        // Arrange
        using var testAgent = new TestAgent(_serviceProvider);
        var url = new Uri("https://httpbin.org/delay/1");

        // Act
        var session = await testAgent.RunAsync(url, settings: null).ConfigureAwait(false);

        // Assert
        Assert.That(session, Is.Not.Null);
        Assert.That(session.TestSettings, Is.Not.Null);
        Assert.That(session.TestSettings.ContinueOnFailure, Is.False);
#if DEBUG
        Assert.That(session.TestSettings.Timeout, Is.EqualTo(TimeSpan.FromMinutes(30)));
#else
        Assert.That(session.TestSettings.Timeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
#endif
    }

    [Test]
    public async Task TestAgentWithCustomTestSettingsRetainsValues()
    {
        // Arrange
        using var testAgent = new TestAgent(_serviceProvider);
        var url = new Uri("https://httpbin.org/delay/1");
        var customSettings = new TestSettings
        {
            ContinueOnFailure = true,
            Timeout = TimeSpan.FromMinutes(2)
        };

        // Act
        var session = await testAgent.RunAsync(url, customSettings).ConfigureAwait(false);

        // Assert
        Assert.That(session, Is.Not.Null);
        Assert.That(session.TestSettings, Is.Not.Null);
        Assert.That(session.TestSettings.ContinueOnFailure, Is.True);
        Assert.That(session.TestSettings.Timeout, Is.EqualTo(TimeSpan.FromMinutes(2)));
    }

    [Test]
    public async Task TestAgentWithDefaultTestSettingsUsesCorrectDefaults()
    {
        // Arrange
        using var testAgent = new TestAgent(_serviceProvider);
        var url = new Uri("https://httpbin.org/delay/1");
        var defaultSettings = new TestSettings(); // Should use defaults

        // Act
        var session = await testAgent.RunAsync(url, defaultSettings).ConfigureAwait(false);

        // Assert
        Assert.That(session, Is.Not.Null);
        Assert.That(session.TestSettings, Is.Not.Null);
        Assert.That(session.TestSettings.ContinueOnFailure, Is.False);
#if DEBUG
        Assert.That(session.TestSettings.Timeout, Is.EqualTo(TimeSpan.FromMinutes(30)));
#else
        Assert.That(session.TestSettings.Timeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
#endif
    }

    [Test]
    public async Task TestAgentSessionAlwaysHasTestSettingsRegardlessOfResult()
    {
        // Arrange
        using var testAgent = new TestAgent(_serviceProvider);
        // Use a valid URL but don't set up any test components to potentially cause issues
        var url = new Uri("https://httpbin.org/delay/1");
        var customSettings = new TestSettings
        {
            ContinueOnFailure = true,
            Timeout = TimeSpan.FromMinutes(5)
        };

        // Act
        var session = await testAgent.RunAsync(url, customSettings).ConfigureAwait(false);

        // Assert
        Assert.That(session, Is.Not.Null);
        Assert.That(session.TestSettings, Is.Not.Null, "TestSettings should always be present");
        Assert.That(session.TestSettings.ContinueOnFailure, Is.True);
        Assert.That(session.TestSettings.Timeout, Is.EqualTo(TimeSpan.FromMinutes(5)));
    }
}