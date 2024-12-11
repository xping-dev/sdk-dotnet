using System.ComponentModel.DataAnnotations;

namespace Xping.Sdk.Shared.UnitTests;

public sealed class EnumExtensionTests
{
    public enum Temp
    {
        [Display(Name = "Aaa")]
        A,
        [Display]
        B,
        C
    }

    [Test]
    public void GetDisplayNameThrowsArgumentNullExceptionWhenInvokedOnNull()
    {
        // Arrange
        Enum? enumValue = null;

        // Act && Assert
        Assert.Throws<ArgumentNullException>(() => enumValue!.GetDisplayName());
    }

    [Test]
    public void GetDisplayNameReturnsDisplayAttribute()
    {
        // Arrange
        Temp temp = Temp.A;

        // Act
        var displayName = temp.GetDisplayName();

        // Assert
        Assert.That(displayName, Is.EqualTo("Aaa"));
    }

    [Test]
    public void GetDisplayNameReturnsEnumNameWhenDisplayAttributeNameIsMissing()
    {
        // Arrange
        Temp temp = Temp.B;

        // Act
        var displayName = temp.GetDisplayName();

        // Assert
        Assert.That(displayName, Is.EqualTo("B"));
    }

    [Test]
    public void GetDisplayNameReturnsEnumNameWhenDisplayAttributeIsMissing()
    {
        // Arrange
        Temp temp = Temp.C;

        // Act
        var displayName = temp.GetDisplayName();

        // Assert
        Assert.That(displayName, Is.EqualTo("C"));
    }
}
