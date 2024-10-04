using Xping.Sdk.Core.Common;

namespace Xping.Sdk.Core.UnitTests.Common;

public sealed class PropertyBagValueTests
{
    [Test]
    public void ShouldNotThrowWhenNonReferenceTypeIsUsed()
    {
        // Arrange
        const int value = 5;

        // Act
        var propertyBagValue = new PropertyBagValue<int>(value);

        // Assert
        Assert.That(propertyBagValue.Value, Is.EqualTo(value));
    }

    [Test]
    public void ShouldNotThrowWhenReferenceTypeIsUsed()
    {
        // Arrange
        object value = "test";

        // Act
        var propertyBagValue = new PropertyBagValue<object>(value);

        // Assert
        Assert.That(propertyBagValue.Value, Is.EqualTo(value));
    }

    [Test]
    public void EqualShouldReturnTrueWhenTwoInstancesAreEqual()
    {
        // Arrange
        const string value1 = "test1";
        const string value2 = "test1";

        var propertyBagValue1 = new PropertyBagValue<string>(value1);
        var propertyBagValue2 = new PropertyBagValue<string>(value2);

        // Act
        bool result = propertyBagValue1.Equals(propertyBagValue2);

        Assert.That(result, Is.True);
    }

    [Test]
    public void EqualShouldReturnFalseWhenTwoInstancesAreNotEqual()
    {
        // Arrange
        const string value1 = "test1";
        const string value2 = "test2";

        var propertyBagValue1 = new PropertyBagValue<string>(value1);
        var propertyBagValue2 = new PropertyBagValue<string>(value2);

        // Act
        bool result = propertyBagValue1.Equals(propertyBagValue2);

        Assert.That(result, Is.False);
    }

    [Test]
    public void EqualShouldReturnTrueWhenTwoInstancesAreEqualAndOneIsCastedToObject()
    {
        // Arrange
        const string value1 = "test1";
        const string value2 = "test1";

        var propertyBagValue1 = new PropertyBagValue<string>(value1);
        object propertyBagValue2 = new PropertyBagValue<string>(value2);

        // Act
        bool result = propertyBagValue1.Equals(propertyBagValue2);

        Assert.That(result, Is.True);
    }

    [Test]
    public void EqualShouldReturnTrueWhenTwoInstancesAreEqualAndBothAreCastedToObject()
    {
        // Arrange
        const string value1 = "test1";
        const string value2 = "test1";

        object propertyBagValue1 = new PropertyBagValue<string>(value1);
        object propertyBagValue2 = new PropertyBagValue<string>(value2);

        // Act
        bool result = propertyBagValue1.Equals(propertyBagValue2);

        Assert.That(result, Is.True);
    }

    [Test]
    public void EqualShouldReturnFalseWhenTwoInstancesAreNotEqualAndOneIsCastedToObject()
    {
        // Arrange
        const string value1 = "test1";
        const string value2 = "test2";

        var propertyBagValue1 = new PropertyBagValue<string>(value1);
        object propertyBagValue2 = new PropertyBagValue<string>(value2);

        // Act
        bool result = propertyBagValue1.Equals(propertyBagValue2);

        Assert.That(result, Is.False);
    }

    [Test]
    public void EqualShouldReturnFalseWhenTwoInstancesAreNotEqualAndBothAreCastedToObject()
    {
        // Arrange
        const string value1 = "test1";
        const string value2 = "test2";

        object propertyBagValue1 = new PropertyBagValue<string>(value1);
        object propertyBagValue2 = new PropertyBagValue<string>(value2);

        // Act
        bool result = propertyBagValue1.Equals(propertyBagValue2);

        Assert.That(result, Is.False);
    }
}
