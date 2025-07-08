/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Core;

namespace Xping.Sdk.Core.UnitTests;

[TestFixture]
public sealed class XpingAttributeTests
{
    [Test]
    public void ConstructorWithValidIdentifierSetsProperty()
    {
        // Arrange
        const string identifier = "homepage-test-001";

        // Act
        var attribute = new XpingAttribute(identifier);

        // Assert
        Assert.That(attribute.Identifier, Is.EqualTo(identifier));
    }

    [Test]
    public void ConstructorWithNullIdentifierThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new XpingAttribute(null!));
    }

    [Test]
    public void ConstructorWithEmptyIdentifierThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new XpingAttribute(string.Empty));
    }

    [Test]
    public void ConstructorWithWhitespaceIdentifierThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new XpingAttribute("   "));
    }

    [Test]
    public void AttributeUsageAllowsMethodAndClassTargets()
    {
        // Arrange
        var attributeType = typeof(XpingAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        Assert.That(attributeUsage, Is.Not.Null);
        Assert.That(attributeUsage.ValidOn, Is.EqualTo(AttributeTargets.Method | AttributeTargets.Class));
        Assert.That(attributeUsage.AllowMultiple, Is.False);
        Assert.That(attributeUsage.Inherited, Is.True);
    }

    [Test]
    public void CanBeAppliedToTestMethod()
    {
        // This test verifies that the attribute can be applied to a method
        // The actual application is tested through reflection
        var methodInfo = typeof(TestClassWithXpingAttribute).GetMethod(nameof(TestClassWithXpingAttribute.TestMethodWithAttribute));
        
        Assert.That(methodInfo, Is.Not.Null);
        
        var xpingAttribute = methodInfo.GetCustomAttributes(typeof(XpingAttribute), false)
            .Cast<XpingAttribute>()
            .FirstOrDefault();
        
        Assert.That(xpingAttribute, Is.Not.Null);
        Assert.That(xpingAttribute.Identifier, Is.EqualTo("method-test-id"));
    }

    [Test]
    public void CanBeAppliedToTestClass()
    {
        // This test verifies that the attribute can be applied to a class
        var classType = typeof(TestClassWithXpingAttribute);
        
        var xpingAttribute = classType.GetCustomAttributes(typeof(XpingAttribute), false)
            .Cast<XpingAttribute>()
            .FirstOrDefault();
        
        Assert.That(xpingAttribute, Is.Not.Null);
        Assert.That(xpingAttribute.Identifier, Is.EqualTo("class-test-id"));
    }
}

[Xping("class-test-id")]
internal class TestClassWithXpingAttribute
{
    [Xping("method-test-id")]
    public static void TestMethodWithAttribute()
    {
        // Test method for attribute testing
    }

    public static void TestMethodWithoutAttribute()
    {
        // Test method without attribute
    }
}
