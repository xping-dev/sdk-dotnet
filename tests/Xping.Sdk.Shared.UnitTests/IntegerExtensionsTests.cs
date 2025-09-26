/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using NUnit.Framework;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Shared.UnitTests;

[TestFixture]
public sealed class IntegerExtensionsTests
{
    [Test]
    [TestCase(1, "1st")]
    [TestCase(2, "2nd")]
    [TestCase(3, "3rd")]
    [TestCase(4, "4th")]
    [TestCase(10, "10th")]
    [TestCase(11, "11th")]  // Special case - should be 11th, not 11st
    [TestCase(12, "12th")]  // Special case - should be 12th, not 12nd
    [TestCase(13, "13th")]  // Special case - should be 13th, not 13rd
    [TestCase(21, "21st")]
    [TestCase(22, "22nd")]
    [TestCase(23, "23rd")]
    [TestCase(101, "101st")]
    [TestCase(111, "111th")]  // Special case - should be 111th, not 111st
    [TestCase(121, "121st")]
    [TestCase(1000, "1000th")]
    [TestCase(1001, "1001st")]
    public void ToOrdinalReturnsCorrectOrdinalFormat(int index, string expected)
    {
        // Act
        var result = index.ToOrdinal();

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    [TestCase(0, "0th")]
    [TestCase(-1, "-1st")]  // Fixed: negative numbers follow same rules based on last digit
    [TestCase(-2, "-2nd")]  // Fixed: negative numbers follow same rules based on last digit
    [TestCase(-11, "-11th")]
    public void ToOrdinalHandlesEdgeCases(int index, string expected)
    {
        // Act
        var result = index.ToOrdinal();

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }
}
