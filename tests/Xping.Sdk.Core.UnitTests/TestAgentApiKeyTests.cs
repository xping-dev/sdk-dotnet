/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using NUnit.Framework;
using Xping.Sdk.Core;

namespace Xping.Sdk.Core.UnitTests;

[TestFixture]
public class TestAgentApiKeyTests
{
    [Test]
    public void ApiKeyWhenSetExplicitlyReturnsSetValue()
    {
        // Arrange
        using var testAgent = new TestAgent(serviceProvider: null!);
        const string expectedApiKey = "test-api-key-123";

        // Act
        testAgent.ApiKey = expectedApiKey;

        // Assert
        Assert.That(testAgent.ApiKey, Is.EqualTo(expectedApiKey));
    }

    [Test]
    public void ApiKeyWhenNotSetReturnsEnvironmentVariable()
    {
        // Arrange
        using var testAgent = new TestAgent(serviceProvider: null!);
        const string expectedApiKey = "env-api-key-456";
        
        // Set environment variable
        Environment.SetEnvironmentVariable("XPING_API_KEY", expectedApiKey);
        
        try
        {
            // Act
            var actualApiKey = testAgent.ApiKey;

            // Assert
            Assert.That(actualApiKey, Is.EqualTo(expectedApiKey));
        }
        finally
        {
            // Clean up
            Environment.SetEnvironmentVariable("XPING_API_KEY", null);
        }
    }

    [Test]
    public void ApiKeyWhenExplicitlySetToNullReturnsEnvironmentVariable()
    {
        // Arrange
        using var testAgent = new TestAgent(serviceProvider: null!);
        const string expectedApiKey = "env-api-key-789";
        
        // Set environment variable
        Environment.SetEnvironmentVariable("XPING_API_KEY", expectedApiKey);
        
        try
        {
            // Act
            testAgent.ApiKey = null;
            var actualApiKey = testAgent.ApiKey;

            // Assert
            Assert.That(actualApiKey, Is.EqualTo(expectedApiKey));
        }
        finally
        {
            // Clean up
            Environment.SetEnvironmentVariable("XPING_API_KEY", null);
        }
    }

    [Test]
    public void ApiKeyWhenNeitherSetNorEnvironmentVariableReturnsNull()
    {
        // Arrange
        using var testAgent = new TestAgent(serviceProvider: null!);
        
        // Ensure environment variable is not set
        Environment.SetEnvironmentVariable("XPING_API_KEY", null);
        
        // Act
        var actualApiKey = testAgent.ApiKey;

        // Assert
        Assert.That(actualApiKey, Is.Null);
    }

    [Test]
    public void ApiKeyExplicitValueTakesPrecedenceOverEnvironmentVariable()
    {
        // Arrange
        using var testAgent = new TestAgent(serviceProvider: null!);
        const string explicitApiKey = "explicit-api-key";
        const string envApiKey = "env-api-key";
        
        // Set environment variable
        Environment.SetEnvironmentVariable("XPING_API_KEY", envApiKey);
        
        try
        {
            // Act
            testAgent.ApiKey = explicitApiKey;
            var actualApiKey = testAgent.ApiKey;

            // Assert
            Assert.That(actualApiKey, Is.EqualTo(explicitApiKey));
        }
        finally
        {
            // Clean up
            Environment.SetEnvironmentVariable("XPING_API_KEY", null);
        }
    }
}
