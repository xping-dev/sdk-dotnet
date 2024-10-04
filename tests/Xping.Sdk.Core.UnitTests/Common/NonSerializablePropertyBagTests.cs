using Xping.Sdk.Core.Common;

namespace Xping.Sdk.Core.UnitTests.Common;

public sealed class NonSerializablePropertyBagTests
{
    [Test]
    public void ShouldNotThrowWhenNonReferenceTypeIsUsed()
    {
        // Arrange
        const int value = 5;

        // Act
        var nonSerializable = new NonSerializable<int>(value);
        
        // Assert
        Assert.That(nonSerializable.Value, Is.EqualTo(value));
    }

    [Test]
    public void ShouldNotThrowWhenReferenceTypeIsUsed()
    {
        // Arrange
        object value = "test";

        // Act
        var nonSerializable = new NonSerializable<object>(value);

        // Assert
        Assert.That(nonSerializable.Value, Is.EqualTo(value));
    }

    [Test]
    public void EqualShouldReturnTrueWhenTwoInstancesAreEqual()
    {
        // Arrange
        const string value1 = "test1";
        const string value2 = "test1";

        var nonSerializable1 = new NonSerializable<string>(value1);
        var nonSerializable2 = new NonSerializable<string>(value2);

        // Act
        bool result = nonSerializable1.Equals(nonSerializable2);

        Assert.That(result, Is.True);
    }

    [Test]
    public void EqualShouldReturnFalseWhenTwoInstancesAreNotEqual()
    {
        // Arrange
        const string value1 = "test1";
        const string value2 = "test2";

        var nonSerializable1 = new NonSerializable<string>(value1);
        var nonSerializable2 = new NonSerializable<string>(value2);

        // Act
        bool result = nonSerializable1.Equals(nonSerializable2);

        Assert.That(result, Is.False);
    }

    [Test]
    public void EqualShouldReturnTrueWhenTwoInstancesAreEqualAndOneIsCastedToObject()
    {
        // Arrange
        const string value1 = "test1";
        const string value2 = "test1";

        var nonSerializable1 = new NonSerializable<string>(value1);
        object nonSerializable2 = new NonSerializable<string>(value2);

        // Act
        bool result = nonSerializable1.Equals(nonSerializable2);

        Assert.That(result, Is.True);
    }

    [Test]
    public void EqualShouldReturnTrueWhenTwoInstancesAreEqualAndBothAreCastedToObject()
    {
        // Arrange
        const string value1 = "test1";
        const string value2 = "test1";

        object nonSerializable1 = new NonSerializable<string>(value1);
        object nonSerializable2 = new NonSerializable<string>(value2);

        // Act
        bool result = nonSerializable1.Equals(nonSerializable2);

        Assert.That(result, Is.True);
    }

    [Test]
    public void EqualShouldReturnFalseWhenTwoInstancesAreNotEqualAndOneIsCastedToObject()
    {
        // Arrange
        const string value1 = "test1";
        const string value2 = "test2";

        var nonSerializable1 = new NonSerializable<string>(value1);
        object nonSerializable2 = new NonSerializable<string>(value2);

        // Act
        bool result = nonSerializable1.Equals(nonSerializable2);

        Assert.That(result, Is.False);
    }

    [Test]
    public void EqualShouldReturnFalseWhenTwoInstancesAreNotEqualAndBothAreCastedToObject()
    {
        // Arrange
        const string value1 = "test1";
        const string value2 = "test2";

        object nonSerializable1 = new NonSerializable<string>(value1);
        object nonSerializable2 = new NonSerializable<string>(value2);

        // Act
        bool result = nonSerializable1.Equals(nonSerializable2);

        Assert.That(result, Is.False);
    }
}
