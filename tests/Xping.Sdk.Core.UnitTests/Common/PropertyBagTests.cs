using Xping.Sdk.Core.Common;

namespace Xping.Sdk.UnitTests.Common;

public sealed class PropertyBagTests
{
    [Test]
    public void ShouldIgnoreNullValue()
    {
        // Arrange
        var propertyBag = new PropertyBag<object>(properties: new Dictionary<PropertyBagKey, object>
        {
            { new PropertyBagKey("key"), null! }
        });

        // Assert
        Assert.That(propertyBag.Keys, Is.Empty);
        Assert.That(propertyBag.Count, Is.EqualTo(0));
    }

    [Test]
    public void AddOrUpdatePropertyShouldIgnoreNullValue()
    {
        // Arrange
        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperty(new PropertyBagKey("key"), null!);

        // Assert
        Assert.That(propertyBag.Keys, Is.Empty);
        Assert.That(propertyBag.Count, Is.EqualTo(0));
    }

    [Test]
    public void AddOrUpdatePropertiesShouldIgnoreNullValues()
    {
        // Arrange
        const int expectedCount = 1;

        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperties(properties: new Dictionary<PropertyBagKey, object>
        {
            { new PropertyBagKey("key1"), null! },
            { new PropertyBagKey("key2"), new object() },
            { new PropertyBagKey("key3"), null! },
            { new PropertyBagKey("key4"), null! }
        });

        // Assert
        Assert.That(propertyBag.Count, Is.EqualTo(expectedCount));
    }

    [Test]
    public void ShouldBeEmptyWhenInstantiatedWithDefaultCtor()
    {
        // Arrange
        var propertyBag = new PropertyBag<IPropertyBagValue>();

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(propertyBag.Keys, Is.Empty);
            Assert.That(propertyBag.Count, Is.EqualTo(0));
        });
    }

    [Test]
    public void ShouldBeEmptyWhenInstantiatedWithEmptyProperties()
    {
        // Arrange
        var propertyBag = new PropertyBag<IPropertyBagValue>(
            properties: (Dictionary<PropertyBagKey, IPropertyBagValue>)([]));

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(propertyBag.Keys, Is.Empty);
            Assert.That(propertyBag.Count, Is.EqualTo(0));
        });
    }

    [Test]
    public void ShouldNotBeEmptyWhenInstantiatedWithNotEmptyProperties()
    {
        // Arrange
        var propertyBag = new PropertyBag<object>(properties: new Dictionary<PropertyBagKey, object>
        {
            { new PropertyBagKey("key"), new object() }
        });

        // Assert
        Assert.That(propertyBag.Keys, Is.Not.Empty);
        Assert.That(propertyBag.Count, Is.Not.EqualTo(0));
    }

    [Test]
    public void ShouldHaveOneItemWhenAddedOneItem()
    {
        // Arrange
        const int expectedItemsCount = 1;
        const string keyName = "key";

        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperty(new PropertyBagKey(keyName), new object());

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(propertyBag.Keys, Has.Count.EqualTo(expectedItemsCount));
            Assert.That(propertyBag.Count, Is.EqualTo(expectedItemsCount));
        });
    }

    [Test]
    public void ShouldHaveOneItemWhenUpdatedItemAfterAdd()
    {
        // Arrange
        const int expectedItemsCount = 1;
        var key = new PropertyBagKey("key");
        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperty(key, new object());
        propertyBag.AddOrUpdateProperty(key, new object());

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(propertyBag.Keys, Has.Count.EqualTo(expectedItemsCount));
            Assert.That(propertyBag.Count, Is.EqualTo(expectedItemsCount));
        });
    }

    [Test]
    public void AddOrUpdatePropertyShouldUpdatedValueWithNewValue()
    {
        // Arrange
        var key = new PropertyBagKey("key");
        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperty(key, new object());
        propertyBag.AddOrUpdateProperty(key, new string(""));

        // Assert
        Assert.That(propertyBag.GetProperty(key), Is.TypeOf<string>());
    }

    [Test]
    public void ContainsKeyShouldReturnTrueWhenKeyIsFound()
    {
        // Arrange
        var key = new PropertyBagKey("key");
        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperty(key, new object());

        // Assert
        Assert.That(propertyBag.ContainsKey(key), Is.True);
    }

    [Test]
    public void ContainsKeyShouldReturnFalseWhenKeyIsNotFound()
    {
        // Arrange
        var key = new PropertyBagKey("key");
        var newKey = new PropertyBagKey("newKey");
        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperty(key, new object());

        // Assert
        Assert.That(propertyBag.ContainsKey(newKey), Is.False);
    }

    [Test]
    public void AddOrUpdatePropertyShouldThrowExceptionWhenKeyIsNull()
    {
        // Arrange
        PropertyBagKey key = null!;
        var propertyBag = new PropertyBag<object>();

        // Assert
        Assert.Throws<ArgumentNullException>(() => propertyBag.AddOrUpdateProperty(key, new object()));
    }

    [Test]
    public void AddOrUpdatePropertyShouldNotThrowExceptionWhenValueIsNull()
    {
        // Arrange
        var key = new PropertyBagKey("key");
        var propertyBag = new PropertyBag<object>();

        // Assert
        Assert.DoesNotThrow(() => propertyBag.AddOrUpdateProperty(key, null!));
    }

    [Test]
    public void AddOrUpdatePropertiesShouldThrowExceptionWhenPropertiesParameterIsNull()
    {
        // Arrange
        Dictionary<PropertyBagKey, object> properties = null!;
        var propertyBag = new PropertyBag<object>();

        // Assert
        Assert.Throws<ArgumentNullException>(() => propertyBag.AddOrUpdateProperties(properties));
    }

    [Test]
    public void ShouldHaveCorrectNumberOfItemsWhenNewItemsAreAdded()
    {
        // Arrange
        var properties = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key"), new object() },
            { new PropertyBagKey("yek"), new object() }
        };
        int expectedItemsCount = properties.Count;
        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperties(properties);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(propertyBag.Keys, Has.Count.EqualTo(expectedItemsCount));
            Assert.That(propertyBag.Count, Is.EqualTo(expectedItemsCount));
        });
    }

    [Test]
    public void ShouldNotThrowWhenDuplicateKeyIsAddOrUpdated()
    {
        // Arrange
        var key = new PropertyBagKey("key");
        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperty(key, new object());

        // Assert
        Assert.DoesNotThrow(() => propertyBag.AddOrUpdateProperty(key, new object()));
    }

    [Test]
    public void TryGetPropertyShouldReturnTrueWhenPropertyExist()
    {
        // Arrange
        var key = new PropertyBagKey("key");
        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperty(key, new object());

        // Assert
        Assert.That(propertyBag.TryGetProperty(key, out _), Is.True);
    }

    [Test]
    public void TryGetPropertyShouldReturnFalseWhenPropertyDoesNotExist()
    {
        // Arrange
        var key = new PropertyBagKey("key");
        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperty(key, new object());

        // Assert
        Assert.That(propertyBag.TryGetProperty(new PropertyBagKey("not_foudn"), out _), Is.False);
    }

    [Test]
    public void TryGetPropertyShouldReturnNullWhenPropertyDoesNotExist()
    {
        // Arrange
        var key = new PropertyBagKey("key");
        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperty(key, new object());
        propertyBag.TryGetProperty(new PropertyBagKey("not_foudn"), out var value);

        // Assert
        Assert.That(value, Is.Null);
    }

    [Test]
    public void GenericTryGetPropertyShouldReturnTrueWhenPropertyExist()
    {
        // Arrange
        var key = new PropertyBagKey("key");
        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperty(key, new object());

        // Assert
        Assert.That(propertyBag.TryGetProperty<object>(key, out _), Is.True);
    }

    [Test]
    public void GenericTryGetPropertyShouldReturnFalseWhenPropertyDoesNotExist()
    {
        // Arrange
        var key = new PropertyBagKey("key");
        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperty(key, new object());

        // Assert
        Assert.That(propertyBag.TryGetProperty<object>(new PropertyBagKey("not_foudn"), out _), Is.False);
    }

    [Test]
    public void GenericTryGetPropertyShouldReturnNullWhenPropertyDoesNotExist()
    {
        // Arrange
        var key = new PropertyBagKey("key");
        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperty(key, new object());
        propertyBag.TryGetProperty<object>(new PropertyBagKey("not_foudn"), out var value);

        // Assert
        Assert.That(value, Is.Null);
    }

    [Test]
    public void GenericTryGetPropertyShouldNotThrowInvalidCastExceptionWhenTypeMismatch()
    {
        // Arrange
        var key = new PropertyBagKey("key");
        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperty(key, new object());

        // Assert
        Assert.DoesNotThrow(() => propertyBag.TryGetProperty<string>(key, out var value));
    }

    [Test]
    public void GetPropertyShouldThrowExceptionWhenKeyIsNull()
    {
        // Arrange
        PropertyBagKey key = null!;
        var propertyBag = new PropertyBag<object>();

        // Assert
        Assert.Throws<ArgumentNullException>(() => propertyBag.GetProperty(key));
    }

    [Test]
    public void GetPropertyShouldThrowKeyNotFoundExceptionWhenKeyDoesNotExist()
    {
        // Arrange
        var propertyBag = new PropertyBag<object>();

        // Assert            
        Assert.Throws<KeyNotFoundException>(() => propertyBag.GetProperty(new PropertyBagKey("no_found")));
    }

    [Test]
    public void GenericGetPropertyShouldThrowExceptionWhenKeyIsNull()
    {
        // Arrange
        PropertyBagKey key = null!;
        var propertyBag = new PropertyBag<object>();

        // Assert
        Assert.Throws<ArgumentNullException>(() => propertyBag.GetProperty<object>(key));
    }

    [Test]
    public void GenericGetPropertyShouldThrowKeyNotFoundExceptionWhenKeyDoesNotExist()
    {
        // Arrange
        var propertyBag = new PropertyBag<object>();

        // Assert            
        Assert.Throws<KeyNotFoundException>(() => propertyBag.GetProperty<object>(new PropertyBagKey("no_found")));
    }

    [Test]
    public void GenericGetPropertyShouldThrowInvalidCastExceptionWhenTypeMismatch()
    {
        // Arrange
        var key = new PropertyBagKey("key");
        var propertyBag = new PropertyBag<object>();

        // Act
        propertyBag.AddOrUpdateProperty(key, new object());

        // Assert            
        Assert.Throws<InvalidCastException>(() => propertyBag.GetProperty<string>(key));
    }

    [Test]
    public void ClearShouldRemoveSpecifiedProperty()
    {
        var key = new PropertyBagKey("key");
        var yek = new PropertyBagKey("yek");

        // Arrange
        var propertyBag = new PropertyBag<object>(new Dictionary<PropertyBagKey, object>
        {
            { key, new object() },
            { yek, new object() }
        });

        // Act
        propertyBag.Clear(new PropertyBagKey("yek"));

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(propertyBag.Keys.First(), Is.EqualTo(key));
            Assert.That(propertyBag.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void ClearShouldThrowArgumentNullExpcetionWhenKeyIsNull()
    {
        // Arrange
        var propertyBag = new PropertyBag<object>();

        // Assert
        Assert.Throws<ArgumentNullException>(() => propertyBag.Clear(key: null!));
    }

    [Test]
    public void ClearShouldRemoveAllPropertes()
    {
        // Arrange
        var propertyBag = new PropertyBag<object>(new Dictionary<PropertyBagKey, object>
        {
            { new PropertyBagKey("key"), new object() },
            { new PropertyBagKey("yek"), new object() }
        });

        // Act
        propertyBag.Clear();

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(propertyBag.Keys, Is.Empty);
            Assert.That(propertyBag.Count, Is.EqualTo(0));
        });
    }

    [Test]
    public void EqaulShouldReturnTrueWhenTwoInstancesAreEqaul()
    {
        // Arrange
        var properties1 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key"), "value" }
        };
        var properties2 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key"), "value" }
        };

        var pBag1 = new PropertyBag<object>(properties1);
        var pBag2 = new PropertyBag<object>(properties2);

        // Act
        bool result = pBag1.Equals(pBag2);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void EqaulShouldReturnTrueWhenTwoInstancesAreEqaulAndOneIsCastedToObject()
    {
        // Arrange
        var properties1 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key"), "value" }
        };
        var properties2 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key"), "value" }
        };

        var pBag1 = new PropertyBag<object>(properties1);
        object pBag2 = new PropertyBag<object>(properties2);

        // Act
        bool result = pBag1.Equals(pBag2);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void EqaulShouldReturnTrueWhenTwoInstancesAreEqaulAndBothAreCastedToObject()
    {
        // Arrange
        var properties1 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key"), "value" }
        };
        var properties2 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key"), "value" }
        };

        object pBag1 = new PropertyBag<object>(properties1);
        object pBag2 = new PropertyBag<object>(properties2);

        // Act
        bool result = pBag1.Equals(pBag2);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void EqaulShouldReturnFalseWhenTwoInstancesHaveDifferentKeysAndTheSameValues()
    {
        // Arrange
        var properties1 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key1"), "value" }
        };
        var properties2 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key2"), "value" }
        };

        object pBag1 = new PropertyBag<object>(properties1);
        object pBag2 = new PropertyBag<object>(properties2);

        // Act
        bool result = pBag1.Equals(pBag2);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void EqaulShouldReturnFalseWhenTwoInstancesHaveDifferentValuesAndTheSameKeys()
    {
        // Arrange
        var properties1 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key"), "value1" }
        };
        var properties2 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key"), "value2" }
        };

        object pBag1 = new PropertyBag<object>(properties1);
        object pBag2 = new PropertyBag<object>(properties2);

        // Act
        bool result = pBag1.Equals(pBag2);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void EqaulShouldReturnFalseWhenTwoInstancesHaveDifferentKeysAndDifferentValues()
    {
        // Arrange
        var properties1 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key1"), "value1" }
        };
        var properties2 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key2"), "value2" }
        };

        object pBag1 = new PropertyBag<object>(properties1);
        object pBag2 = new PropertyBag<object>(properties2);

        // Act
        bool result = pBag1.Equals(pBag2);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void EqaulOperatorShouldReturnTrueWhenTwoInstancesAreEqaul()
    {
        // Arrange
        var properties1 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key"), "value" }
        };
        var properties2 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key"), "value" }
        };

        var pBag1 = new PropertyBag<object>(properties1);
        var pBag2 = new PropertyBag<object>(properties2);

        // Act
        bool result = pBag1 == pBag2;

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void InequalityOperatorShouldReturnFalseWhenTwoInstancesAreEqaul()
    {
        // Arrange
        var properties1 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key"), "value" }
        };
        var properties2 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key"), "value" }
        };

        var pBag1 = new PropertyBag<object>(properties1);
        var pBag2 = new PropertyBag<object>(properties2);

        // Act
        bool result = pBag1 != pBag2;

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void InequalityOperatorShouldReturnTrueWhenTwoInstancesAreNotEqual()
    {
        // Arrange
        var properties1 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key"), "value1" }
        };
        var properties2 = new Dictionary<PropertyBagKey, object>()
        {
            { new PropertyBagKey("key"), "value2" }
        };

        var pBag1 = new PropertyBag<object>(properties1);
        var pBag2 = new PropertyBag<object>(properties2);

        // Act
        bool result = pBag1 != pBag2;

        // Assert
        Assert.That(result, Is.True);
    }
}
