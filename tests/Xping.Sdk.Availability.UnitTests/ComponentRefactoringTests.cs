/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Actions;
using Xping.Sdk.Actions.Configurations;
using Xping.Sdk.Core.Clients.Http;
using Xping.Sdk.Validations.HttpResponse;

namespace Xping.Sdk.UnitTests.Components;

[TestFixture]
internal sealed class ComponentRefactoringTests
{
    [Test]
    public void DnsLookup_NameIsClassNameAndDisplayNameIsUserFriendly()
    {
        // Arrange & Act
        var component = new DnsLookup();

        // Assert
        Assert.That(component.Name, Is.EqualTo(nameof(DnsLookup)));
        Assert.That(component.DisplayName, Is.EqualTo("DNS lookup"));
    }

    [Test]
    public void HttpClientRequestSender_NameIsClassNameAndDisplayNameIsUserFriendly()
    {
        // Arrange & Act
        var component = new HttpClientRequestSender(new HttpClientConfiguration());

        // Assert
        Assert.That(component.Name, Is.EqualTo(nameof(HttpClientRequestSender)));
        Assert.That(component.DisplayName, Is.EqualTo("HTTP Client Request Sender"));
    }

    [Test]
    public void HttpResponseValidator_NameIsClassNameAndDisplayNameIsUserFriendly()
    {
        // Arrange & Act
        var component = new HttpResponseValidator(response => { });

        // Assert
        Assert.That(component.Name, Is.EqualTo(nameof(HttpResponseValidator)));
        Assert.That(component.DisplayName, Is.EqualTo("HTTP Response Validator"));
    }

    [Test]
    public void IPAddressAccessibilityCheck_NameIsClassNameAndDisplayNameIsUserFriendly()
    {
        // Arrange & Act
        var component = new IPAddressAccessibilityCheck(new PingConfiguration());

        // Assert
        Assert.That(component.Name, Is.EqualTo(nameof(IPAddressAccessibilityCheck)));
        Assert.That(component.DisplayName, Is.EqualTo("IP Address Accessibility Check"));
    }
}
