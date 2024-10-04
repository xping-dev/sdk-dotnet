using Xping.Sdk.Core.Common;

namespace Xping.Sdk.UnitTests.Common;

public sealed class PropertyBagKeyTests
{
    [Test]
    public void ToStringShouldReturnKeyValue()
    {
        // Arrange
        const string value = "key";
        var key = new PropertyBagKey(value);

        // Arrange
        string result = key.ToString();

        // Assert
        Assert.That(result, Is.EqualTo(value));
    }

    [Test]
    public void ShouldThrowExceptionWhenArgumentIsNull()
    {
        // Assert
        Assert.Throws<ArgumentException>(() => new PropertyBagKey(key: null!));
    }

    [Test]
    public void ShouldThrowExceptionWhenArgumentIsEmpty()
    {
        // Assert
        Assert.Throws<ArgumentException>(() => new PropertyBagKey(key: string.Empty));
    }

    [Test]
    public void ShouldNotThrowExceptionWhenArgumentIsNull()
    {
        // Assert
        Assert.DoesNotThrow(() => new PropertyBagKey(key: "key"));
    }

    [Test]
    public void EqualShouldReturnTrueWhenTwoInstanceAreEqual()
    {
        // Arrange
        var key1 = new PropertyBagKey("key");
        var key2 = new PropertyBagKey("key");

        // Act
        bool result = key1.Equals(key2);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void EqualShouldReturnTrueWhenTwoInstanceAreEqualAndOneIsCastedToObject()
    {
        // Arrange
        var key1 = new PropertyBagKey("key");
        object key2 = new PropertyBagKey("key");

        // Act
        bool result = key1.Equals(key2);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void EqualShouldReturnTrueWhenTwoInstanceAreEqualAndBothAreCastedToObject()
    {
        // Arrange
        object key1 = new PropertyBagKey("key");
        object key2 = new PropertyBagKey("key");

        // Act
        bool result = key1.Equals(key2);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void EqualShouldReturnFalseWhenTwoInstanceAreNotEqual()
    {
        // Arrange
        var key1 = new PropertyBagKey("key1");
        var key2 = new PropertyBagKey("key2");

        // Act
        bool result = key1.Equals(key2);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void EqualShouldReturnFalseWhenTwoInstanceAreNotEqualAndOneIsCastedToObject()
    {
        // Arrange
        var key1 = new PropertyBagKey("key1");
        object key2 = new PropertyBagKey("key2");

        // Act
        bool result = key1.Equals(key2);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void EqualShouldReturnFalseWhenTwoInstanceAreNotEqualAndBothAreCastedToObject()
    {
        // Arrange
        object key1 = new PropertyBagKey("key1");
        object key2 = new PropertyBagKey("key2");

        // Act
        bool result = key1.Equals(key2);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void EqualOperatorShouldReturnTrueWhenTwoInstanceAreEqual()
    {
        // Arrange
        var key1 = new PropertyBagKey("key");
        var key2 = new PropertyBagKey("key");

        // Act
        bool result = key1 == key2;

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void EqualOperatorShouldReturnFalseWhenTwoInstanceAreNotEqual()
    {
        // Arrange
        var key1 = new PropertyBagKey("key1");
        var key2 = new PropertyBagKey("key2");

        // Act
        bool result = key1 == key2;

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void InequalityOperatorShouldReturnTrueWhenTwoInstanceAreNotEqual()
    {
        // Arrange
        var key1 = new PropertyBagKey("key1");
        var key2 = new PropertyBagKey("key2");

        // Act
        bool result = key1 != key2;

        // Assert
        Assert.That(result, Is.True);
    }
}
