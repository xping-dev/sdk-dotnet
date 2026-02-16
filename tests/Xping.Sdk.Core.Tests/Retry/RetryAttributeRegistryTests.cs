/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.Retry;

namespace Xping.Sdk.Core.Tests.Retry;

public sealed class RetryAttributeRegistryTests
{
    [Fact]
    public void Should_Recognize_All_XUnit_Retry_Attributes()
    {
        // Arrange
        var xunitAttributes = new[]
        {
            "RetryFact",
            "RetryTheory",
            "RetryAttribute",
            "XunitRetryAttribute"
        };

        // Act & Assert
        foreach (var attr in xunitAttributes)
        {
            Assert.True(RetryAttributeRegistry.IsKnownRetryAttribute(attr),
                $"{attr} should be recognized as a known retry attribute");
            Assert.True(RetryAttributeRegistry.XUnitRetryAttributes.Contains(attr),
                $"{attr} should be in XUnit registry");
        }
    }

    [Fact]
    public void Should_Recognize_All_NUnit_Retry_Attributes()
    {
        // Arrange
        var nunitAttributes = new[]
        {
            "Retry",
            "RetryAttribute",
            "NUnit.Framework.RetryAttribute"
        };

        // Act & Assert
        foreach (var attr in nunitAttributes)
        {
            Assert.True(RetryAttributeRegistry.IsKnownRetryAttribute(attr),
                $"{attr} should be recognized as a known retry attribute");
            Assert.True(RetryAttributeRegistry.NUnitRetryAttributes.Contains(attr),
                $"{attr} should be in NUnit registry");
        }
    }

    [Fact]
    public void Should_Recognize_All_MSTest_Retry_Attributes()
    {
        // Arrange
        var mstestAttributes = new[]
        {
            "TestRetry",
            "RetryAttribute",
            "DataTestMethodWithRetry"
        };

        // Act & Assert
        foreach (var attr in mstestAttributes)
        {
            Assert.True(RetryAttributeRegistry.IsKnownRetryAttribute(attr),
                $"{attr} should be recognized as a known retry attribute");
            Assert.True(RetryAttributeRegistry.MSTestRetryAttributes.Contains(attr),
                $"{attr} should be in MSTest registry");
        }
    }

    [Fact]
    public void Should_Be_Case_Insensitive()
    {
        // Arrange
        var variations = new[]
        {
            "retryfact",
            "RETRYFACT",
            "RetryFact",
            "retryFACT"
        };

        // Act & Assert
        foreach (var variation in variations)
        {
            Assert.True(RetryAttributeRegistry.IsKnownRetryAttribute(variation),
                $"{variation} should be recognized (case-insensitive)");
        }
    }

    [Fact]
    public void Should_Return_False_For_Unknown_Attributes()
    {
        // Arrange
        var unknownAttributes = new[]
        {
            "TestMethod",
            "Fact",
            "Theory",
            "Test",
            "UnknownRetry"
        };

        // Act & Assert
        foreach (var attr in unknownAttributes)
        {
            Assert.False(RetryAttributeRegistry.IsKnownRetryAttribute(attr),
                $"{attr} should not be recognized as a retry attribute");
        }
    }

    [Fact]
    public void Should_Return_False_For_Null_Or_Empty_Attribute_Name()
    {
        // Assert
        Assert.False(RetryAttributeRegistry.IsKnownRetryAttribute(null!));
        Assert.False(RetryAttributeRegistry.IsKnownRetryAttribute(string.Empty));
        Assert.False(RetryAttributeRegistry.IsKnownRetryAttribute("   "));
    }

    [Fact]
    public void Should_Register_Custom_XUnit_Retry_Attribute()
    {
        // Arrange
        var customAttribute = "MyCustomXunitRetry_" + Guid.NewGuid();

        // Act
        RetryAttributeRegistry.RegisterCustomRetryAttribute("xunit", customAttribute);

        // Assert
        Assert.True(RetryAttributeRegistry.IsKnownRetryAttribute(customAttribute));
        Assert.Contains(customAttribute, RetryAttributeRegistry.XUnitRetryAttributes);
    }

    [Fact]
    public void Should_Register_Custom_NUnit_Retry_Attribute()
    {
        // Arrange
        var customAttribute = "MyCustomNUnitRetry_" + Guid.NewGuid();

        // Act
        RetryAttributeRegistry.RegisterCustomRetryAttribute("nunit", customAttribute);

        // Assert
        Assert.True(RetryAttributeRegistry.IsKnownRetryAttribute(customAttribute));
        Assert.Contains(customAttribute, RetryAttributeRegistry.NUnitRetryAttributes);
    }

    [Fact]
    public void Should_Register_Custom_MSTest_Retry_Attribute()
    {
        // Arrange
        var customAttribute = "MyCustomMSTestRetry_" + Guid.NewGuid();

        // Act
        RetryAttributeRegistry.RegisterCustomRetryAttribute("mstest", customAttribute);

        // Assert
        Assert.True(RetryAttributeRegistry.IsKnownRetryAttribute(customAttribute));
        Assert.Contains(customAttribute, RetryAttributeRegistry.MSTestRetryAttributes);
    }

    [Fact]
    public void Should_Handle_Case_Insensitive_Framework_Names()
    {
        // Arrange
        var customAttribute = "CaseInsensitiveTest_" + Guid.NewGuid();

        // Act
        RetryAttributeRegistry.RegisterCustomRetryAttribute("XUNIT", customAttribute);

        // Assert
        Assert.True(RetryAttributeRegistry.IsKnownRetryAttribute(customAttribute));
        Assert.Contains(customAttribute, RetryAttributeRegistry.XUnitRetryAttributes);
    }

    [Fact]
    public void Should_Ignore_Registration_With_Null_Or_Empty_Attribute_Name()
    {
        // Act & Assert - should not throw
        RetryAttributeRegistry.RegisterCustomRetryAttribute("xunit", null!);
        RetryAttributeRegistry.RegisterCustomRetryAttribute("xunit", string.Empty);
        RetryAttributeRegistry.RegisterCustomRetryAttribute("xunit", "   ");

        // Verify no invalid entries were added
        Assert.False(RetryAttributeRegistry.IsKnownRetryAttribute(null!));
        Assert.False(RetryAttributeRegistry.IsKnownRetryAttribute(string.Empty));
    }

    [Fact]
    public void Should_Ignore_Registration_With_Invalid_Framework_Name()
    {
        // Arrange
        var customAttribute = "ShouldNotBeRegistered_" + Guid.NewGuid();

        // Act
        RetryAttributeRegistry.RegisterCustomRetryAttribute("InvalidFramework", customAttribute);

        // Assert - should not be in any registry
        Assert.DoesNotContain(customAttribute, RetryAttributeRegistry.XUnitRetryAttributes);
        Assert.DoesNotContain(customAttribute, RetryAttributeRegistry.NUnitRetryAttributes);
        Assert.DoesNotContain(customAttribute, RetryAttributeRegistry.MSTestRetryAttributes);
    }

    [Fact]
    public void Should_Allow_Duplicate_Registration()
    {
        // Arrange
        var customAttribute = "DuplicateTest_" + Guid.NewGuid();

        // Act - register twice
        RetryAttributeRegistry.RegisterCustomRetryAttribute("xunit", customAttribute);
        RetryAttributeRegistry.RegisterCustomRetryAttribute("xunit", customAttribute);

        // Assert - should only be in the set once (HashSet behavior)
        Assert.True(RetryAttributeRegistry.IsKnownRetryAttribute(customAttribute));
    }

    [Fact]
    public void GetAttributesForFramework_Should_Return_XUnit_Attributes()
    {
        // Act
        var attributes = RetryAttributeRegistry.GetAttributesForFramework("xunit");

        // Assert
        Assert.NotNull(attributes);
        Assert.NotEmpty(attributes);
        Assert.Contains("RetryFact", attributes);
        Assert.Contains("RetryTheory", attributes);
    }

    [Fact]
    public void GetAttributesForFramework_Should_Return_NUnit_Attributes()
    {
        // Act
        var attributes = RetryAttributeRegistry.GetAttributesForFramework("nunit");

        // Assert
        Assert.NotNull(attributes);
        Assert.NotEmpty(attributes);
        Assert.Contains("Retry", attributes);
    }

    [Fact]
    public void GetAttributesForFramework_Should_Return_MSTest_Attributes()
    {
        // Act
        var attributes = RetryAttributeRegistry.GetAttributesForFramework("mstest");

        // Assert
        Assert.NotNull(attributes);
        Assert.NotEmpty(attributes);
        Assert.Contains("TestRetry", attributes);
    }

    [Fact]
    public void GetAttributesForFramework_Should_Return_Empty_For_Invalid_Framework()
    {
        // Act
        var attributes = RetryAttributeRegistry.GetAttributesForFramework("invalid");

        // Assert
        Assert.NotNull(attributes);
        Assert.Empty(attributes);
    }

    [Fact]
    public void GetAttributesForFramework_Should_Be_Case_Insensitive()
    {
        // Act
        var attributes1 = RetryAttributeRegistry.GetAttributesForFramework("XUNIT");
        var attributes2 = RetryAttributeRegistry.GetAttributesForFramework("xunit");
        var attributes3 = RetryAttributeRegistry.GetAttributesForFramework("XUnit");

        // Assert
        Assert.Equal(attributes1.Count, attributes2.Count);
        Assert.Equal(attributes2.Count, attributes3.Count);
    }

    [Fact]
    public void IsRegisteredForFramework_Should_Return_True_For_Known_Attributes()
    {
        // Assert
        Assert.True(RetryAttributeRegistry.IsRegisteredForFramework("xunit", "RetryFact"));
        Assert.True(RetryAttributeRegistry.IsRegisteredForFramework("nunit", "Retry"));
        Assert.True(RetryAttributeRegistry.IsRegisteredForFramework("mstest", "TestRetry"));
    }

    [Fact]
    public void IsRegisteredForFramework_Should_Return_False_For_Unknown_Attributes()
    {
        // Assert
        Assert.False(RetryAttributeRegistry.IsRegisteredForFramework("xunit", "UnknownAttribute"));
        Assert.False(RetryAttributeRegistry.IsRegisteredForFramework("nunit", "NotRetry"));
        Assert.False(RetryAttributeRegistry.IsRegisteredForFramework("mstest", "InvalidAttribute"));
    }

    [Fact]
    public void IsRegisteredForFramework_Should_Return_False_For_Null_Or_Empty_Attribute()
    {
        // Assert
        Assert.False(RetryAttributeRegistry.IsRegisteredForFramework("xunit", null!));
        Assert.False(RetryAttributeRegistry.IsRegisteredForFramework("xunit", string.Empty));
        Assert.False(RetryAttributeRegistry.IsRegisteredForFramework("xunit", "   "));
    }

    [Fact]
    public void IsRegisteredForFramework_Should_Be_Case_Insensitive()
    {
        // Assert
        Assert.True(RetryAttributeRegistry.IsRegisteredForFramework("XUNIT", "retryfact"));
        Assert.True(RetryAttributeRegistry.IsRegisteredForFramework("xunit", "RETRYFACT"));
    }

    [Fact]
    public void Should_Have_Default_Attributes_Registered()
    {
        // Assert - verify that defaults are always available
        Assert.True(RetryAttributeRegistry.XUnitRetryAttributes.Count >= 4);
        Assert.True(RetryAttributeRegistry.NUnitRetryAttributes.Count >= 3);
        Assert.True(RetryAttributeRegistry.MSTestRetryAttributes.Count >= 3);
    }

    [Fact]
    public void Should_Support_Multiple_Custom_Attributes_Per_Framework()
    {
        // Arrange
        var customAttributes = new[]
        {
            "CustomRetry1_" + Guid.NewGuid(),
            "CustomRetry2_" + Guid.NewGuid(),
            "CustomRetry3_" + Guid.NewGuid()
        };

        // Act
        foreach (var attr in customAttributes)
        {
            RetryAttributeRegistry.RegisterCustomRetryAttribute("xunit", attr);
        }

        // Assert
        foreach (var attr in customAttributes)
        {
            Assert.True(RetryAttributeRegistry.IsKnownRetryAttribute(attr));
            Assert.Contains(attr, RetryAttributeRegistry.XUnitRetryAttributes);
        }
    }

    [Fact]
    public void Should_Not_Share_Attributes_Between_Frameworks()
    {
        // Arrange
        var xunitOnly = "XUnitOnlyRetry_" + Guid.NewGuid();
        var nunitOnly = "NUnitOnlyRetry_" + Guid.NewGuid();

        // Act
        RetryAttributeRegistry.RegisterCustomRetryAttribute("xunit", xunitOnly);
        RetryAttributeRegistry.RegisterCustomRetryAttribute("nunit", nunitOnly);

        // Assert
        Assert.Contains(xunitOnly, RetryAttributeRegistry.XUnitRetryAttributes);
        Assert.DoesNotContain(xunitOnly, RetryAttributeRegistry.NUnitRetryAttributes);
        Assert.DoesNotContain(xunitOnly, RetryAttributeRegistry.MSTestRetryAttributes);

        Assert.Contains(nunitOnly, RetryAttributeRegistry.NUnitRetryAttributes);
        Assert.DoesNotContain(nunitOnly, RetryAttributeRegistry.XUnitRetryAttributes);
        Assert.DoesNotContain(nunitOnly, RetryAttributeRegistry.MSTestRetryAttributes);
    }
}
