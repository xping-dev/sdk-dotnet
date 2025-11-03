/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace SampleApp.XUnit;

using global::Xunit;

/// <summary>
/// Sample tests demonstrating xUnit adapter usage with Xping SDK.
/// Note: Tests are automatically tracked by configuring the test framework in AssemblyInfo.cs.
/// </summary>
public class SampleTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void PassingTestIsTracked()
    {
        // Arrange
        var value = 42;

        // Act
        var result = value * 2;

        // Assert
        Assert.Equal(84, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Fast")]
    public void AnotherPassingTest()
    {
        // Arrange
        var expected = true;

        // Act & Assert
        Assert.True(expected);
    }

    [Fact(Skip = "Demonstrating skipped test tracking")]
    [Trait("Category", "Integration")]
    public void SkippedTestIsTracked()
    {
        Assert.Fail("This test is skipped");
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(5, 5, 10)]
    [InlineData(0, 0, 0)]
    [Trait("Category", "Unit")]
    public void ParameterizedTestIsTracked(int a, int b, int expected)
    {
        var result = a + b;
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetTestData))]
    [Trait("Category", "Unit")]
    public void MemberDataTestIsTracked(string input, int expectedLength)
    {
        Assert.Equal(expectedLength, input.Length);
    }

    public static TheoryData<string, int> GetTestData()
    {
        return new TheoryData<string, int>
        {
            { "hello", 5 },
            { "world", 5 },
            { "xunit", 5 },
        };
    }
}

/// <summary>
/// Sample tests demonstrating test collections and fixtures.
/// </summary>
[Collection("Sample Collection")]
public class CollectionTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void TestInCollection()
    {
        Assert.True(true);
    }
}

/// <summary>
/// Collection definition for grouping tests.
/// </summary>
[CollectionDefinition("Sample Collection")]
public class SampleCollection
{
}
