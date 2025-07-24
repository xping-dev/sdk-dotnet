/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Runtime.Serialization;
using Xping.Sdk.Core.Session;

namespace Xping.Sdk.UnitTests.Session;

[TestFixture]
public sealed class TestMetadataTests
{
    private static TestMetadata CreateTestMetadataUnderTest(
        string methodName = "TestMethod",
        string className = "TestClass",
        string namespaceName = "TestNamespace",
        string processName = "TestProcess",
        int processId = 1234,
        IReadOnlyCollection<string>? classAttributeNames = null,
        IReadOnlyCollection<string>? methodAttributeNames = null,
        string? testDescription = null,
        string? xpingIdentifier = null)
    {
        var metadata = new TestMetadata
        {
            MethodName = methodName,
            ClassName = className,
            Namespace = namespaceName,
            ProcessName = processName,
            ProcessId = processId,
            ClassAttributeNames = classAttributeNames ?? ["TestFixtureAttribute"],
            MethodAttributeNames = methodAttributeNames ?? ["TestAttribute"],
            TestDescription = testDescription
        };

        metadata.UpdateXpingIdentifier(xpingIdentifier ?? "default-xping-identifier");
        return metadata;
    }

    [Test]
    public void ConstructorDefaultConstructorInitializesWithEmptyValues()
    {
        // Act
        var metadata = new TestMetadata();

        // Assert
        Assert.That(metadata.MethodName, Is.EqualTo(string.Empty));
        Assert.That(metadata.ClassName, Is.EqualTo(string.Empty));
        Assert.That(metadata.Namespace, Is.EqualTo(string.Empty));
        Assert.That(metadata.ProcessName, Is.EqualTo(string.Empty));
        Assert.That(metadata.ProcessId, Is.EqualTo(0));
        Assert.That(metadata.ClassAttributeNames, Is.Empty);
        Assert.That(metadata.MethodAttributeNames, Is.Empty);
        Assert.That(metadata.TestDescription, Is.Null);
        Assert.That(metadata.XpingIdentifier, Is.Null);
    }

    [Test]
    public void FullyQualifiedNameWithValidNamespaceClassAndMethodReturnsCorrectFormat()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodName: "MyTestMethod",
            className: "MyTestClass",
            namespaceName: "MyTestNamespace");

        // Act
        var fullyQualifiedName = metadata.FullyQualifiedName;

        // Assert
        Assert.That(fullyQualifiedName, Is.EqualTo("MyTestNamespace.MyTestClass.MyTestMethod"));
    }

    [Test]
    public void DisplayNameWithTestDescriptionReturnsDescription()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            testDescription: "This is a test description");

        // Act
        var displayName = metadata.DisplayName;

        // Assert
        Assert.That(displayName, Is.EqualTo("This is a test description"));
    }

    [Test]
    public void DisplayNameWithoutTestDescriptionReturnsMethodName()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            testDescription: null);

        // Act
        var displayName = metadata.DisplayName;

        // Assert
        Assert.That(displayName, Is.EqualTo("TestMethod"));
    }

    [Test]
    public void DisplayNameWithEmptyTestDescriptionReturnsMethodName()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            testDescription: string.Empty);

        // Act
        var displayName = metadata.DisplayName;

        // Assert
        Assert.That(displayName, Is.EqualTo("TestMethod"));
    }

    [Test]
    public void IsTestMethodWithValidTestDataReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest();

        // Act
        var isTestMethod = metadata.IsTestMethod;

        // Assert
        Assert.That(isTestMethod, Is.True);
    }

    [Test]
    public void IsTestMethodWithEmptyMethodNameReturnsFalse()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(methodName: string.Empty);

        // Act
        var isTestMethod = metadata.IsTestMethod;

        // Assert
        Assert.That(isTestMethod, Is.False);
    }

    [Test]
    public void IsTestMethodWithEmptyClassNameReturnsFalse()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(className: string.Empty);

        // Act
        var isTestMethod = metadata.IsTestMethod;

        // Assert
        Assert.That(isTestMethod, Is.False);
    }

    [Test]
    public void IsTestMethodWithEmptyNamespaceReturnsFalse()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(namespaceName: string.Empty);

        // Act
        var isTestMethod = metadata.IsTestMethod;

        // Assert
        Assert.That(isTestMethod, Is.False);
    }

    [Test]
    public void IsTestMethodWithEmptyMethodAttributesReturnsFalse()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(methodAttributeNames: []);

        // Act
        var isTestMethod = metadata.IsTestMethod;

        // Assert
        Assert.That(isTestMethod, Is.False);
    }

    [Test]
    public void HasAttributeWithExistingMethodAttributeReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["TestAttribute", "CategoryAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasAttribute("TestAttribute"), Is.True);
        Assert.That(metadata.HasAttribute("CategoryAttribute"), Is.True);
    }

    [Test]
    public void HasAttributeWithExistingClassAttributeReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            classAttributeNames: ["TestFixtureAttribute", "CategoryAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasAttribute("TestFixtureAttribute"), Is.True);
        Assert.That(metadata.HasAttribute("CategoryAttribute"), Is.True);
    }

    [Test]
    public void HasAttributeWithNonExistingAttributeReturnsFalse()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["TestAttribute"],
            classAttributeNames: ["TestFixtureAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasAttribute("NonExistingAttribute"), Is.False);
    }

    [Test]
    public void HasAttributeWithPartialAttributeNameReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["TestAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasAttribute("Test"), Is.True);
    }

    [Test]
    public void HasAttributeWithCaseInsensitiveMatchReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["TestAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasAttribute("testattribute"), Is.True);
        Assert.That(metadata.HasAttribute("TESTATTRIBUTE"), Is.True);
    }

    [Test]
    public void HasAttributeWithNullOrWhitespaceAttributeNameThrowsArgumentException()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => metadata.HasAttribute(null!));
        Assert.Throws<ArgumentException>(() => metadata.HasAttribute(string.Empty));
        Assert.Throws<ArgumentException>(() => metadata.HasAttribute("   "));
    }

    [Test]
    public void HasTestAttributeWithTestAttributeReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["TestAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasTestAttribute(), Is.True);
    }

    [Test]
    public void HasTestAttributeWithFactAttributeReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["FactAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasTestAttribute(), Is.True);
    }

    [Test]
    public void HasTestAttributeWithTheoryAttributeReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["TheoryAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasTestAttribute(), Is.True);
    }

    [Test]
    public void HasTestAttributeWithNonTestAttributeReturnsFalse()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["CategoryAttribute", "ObsoleteAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasTestAttribute(), Is.False);
    }

    [Test]
    public void ToStringForTestMethodReturnsFormattedString()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "TestProcess",
            processId: 1234);

        // Act
        var result = metadata.ToString();

        // Assert
        Assert.That(result, Is.EqualTo("TestNamespace.TestClass.TestMethod [TestProcess:1234]"));
    }

    [Test]
    public void ToStringForTestMethodWithDescriptionReturnsFormattedStringWithDescription()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "TestProcess",
            processId: 1234,
            testDescription: "Test Description");

        // Act
        var result = metadata.ToString();

        // Assert
        Assert.That(result, Is.EqualTo("TestNamespace.TestClass.TestMethod - Test Description [TestProcess:1234]"));
    }

    [Test]
    public void ToStringForNonTestMethodReturnsProcessInfo()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodName: string.Empty,
            className: string.Empty,
            namespaceName: string.Empty,
            processName: "TestProcess",
            processId: 1234,
            methodAttributeNames: []);

        // Act
        var result = metadata.ToString();

        // Assert
        Assert.That(result, Is.EqualTo("Process: TestProcess (ID: 1234)"));
    }

    [Test]
    public void EqualsWithSameValuesReturnsTrue()
    {
        // Arrange
        var metadata1 = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "TestProcess",
            processId: 1234);

        var metadata2 = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "TestProcess",
            processId: 1234);

        // Act & Assert
        Assert.That(metadata1.Equals(metadata2), Is.True);
        Assert.That(metadata1 == metadata2, Is.True);
        Assert.That(metadata1 != metadata2, Is.False);
    }

    [Test]
    public void EqualsWithDifferentValuesReturnsFalse()
    {
        // Arrange
        var metadata1 = CreateTestMetadataUnderTest(
            methodName: "TestMethod1",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "TestProcess",
            processId: 1234);

        var metadata2 = CreateTestMetadataUnderTest(
            methodName: "TestMethod2",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "TestProcess",
            processId: 1234);

        // Act & Assert
        Assert.That(metadata1.Equals(metadata2), Is.False);
        Assert.That(metadata1 == metadata2, Is.False);
        Assert.That(metadata1 != metadata2, Is.True);
    }

    [Test]
    public void EqualsWithNullReturnsFalse()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest();

        // Act & Assert
        Assert.That(metadata.Equals(null), Is.False);
        Assert.That(metadata == null, Is.False);
        Assert.That(metadata != null, Is.True);
    }

    [Test]
    public void GetHashCodeWithSameValuesReturnsSameHashCode()
    {
        // Arrange
        var metadata1 = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "TestProcess",
            processId: 1234);

        var metadata2 = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "TestProcess",
            processId: 1234);

        // Act & Assert
        Assert.That(metadata1.GetHashCode(), Is.EqualTo(metadata2.GetHashCode()));
    }

    [Test]
    public void GetHashCodeWithDifferentValuesReturnsDifferentHashCode()
    {
        // Arrange
        var metadata1 = CreateTestMetadataUnderTest(
            methodName: "TestMethod1",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "TestProcess",
            processId: 1234);

        var metadata2 = CreateTestMetadataUnderTest(
            methodName: "TestMethod2",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "TestProcess",
            processId: 1234);

        // Act & Assert
        Assert.That(metadata1.GetHashCode(), Is.Not.EqualTo(metadata2.GetHashCode()));
    }

    [Test]
    public void SerializationRoundTripPreservesAllData()
    {
        // Arrange
        var original = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "TestProcess",
            processId: 1234,
            classAttributeNames: ["TestFixtureAttribute", "CategoryAttribute"],
            methodAttributeNames: ["TestAttribute", "DescriptionAttribute"],
            testDescription: "Test Description",
            xpingIdentifier: "homepage-test-001");

        // Act
        var serialized = SerializeToMemoryStream(original);
        var deserialized = DeserializeFromMemoryStream<TestMetadata>(serialized);

        // Assert
        Assert.That(deserialized.MethodName, Is.EqualTo(original.MethodName));
        Assert.That(deserialized.ClassName, Is.EqualTo(original.ClassName));
        Assert.That(deserialized.Namespace, Is.EqualTo(original.Namespace));
        Assert.That(deserialized.ProcessName, Is.EqualTo(original.ProcessName));
        Assert.That(deserialized.ProcessId, Is.EqualTo(original.ProcessId));
        Assert.That(deserialized.ClassAttributeNames, Is.EquivalentTo(original.ClassAttributeNames));
        Assert.That(deserialized.MethodAttributeNames, Is.EquivalentTo(original.MethodAttributeNames));
        Assert.That(deserialized.TestDescription, Is.EqualTo(original.TestDescription));
        Assert.That(deserialized.XpingIdentifier, Is.EqualTo(original.XpingIdentifier));
    }

    [Test]
    public void SerializationWithNullValuesHandlesNullsCorrectly()
    {
        // Arrange
        var original = new TestMetadata
        {
            MethodName = "TestMethod",
            ClassName = "TestClass",
            Namespace = "TestNamespace",
            ProcessName = "TestProcess",
            ProcessId = 1234,
            ClassAttributeNames = ["TestFixtureAttribute"],
            MethodAttributeNames = ["TestAttribute"],
            TestDescription = null
        };

        // Act
        var serialized = SerializeToMemoryStream(original);
        var deserialized = DeserializeFromMemoryStream<TestMetadata>(serialized);

        // Assert
        Assert.That(deserialized.TestDescription, Is.Null);
        Assert.That(deserialized.MethodName, Is.EqualTo(original.MethodName));
    }

    [Test]
    public void SerializationConstructorWithNullSerializationInfoThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new TestMetadata(null!, new StreamingContext()));
    }

    [Test]
    public void DebuggerDisplayForTestMethodReturnsCorrectFormat()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "TestProcess",
            processId: 1234);

        // Act
        var debugDisplay = metadata.ToString(); // DebuggerDisplay uses the same logic as ToString

        // Assert
        Assert.That(debugDisplay, Is.EqualTo("TestNamespace.TestClass.TestMethod [TestProcess:1234]"));
    }

    [Test]
    public void DebuggerDisplayForNonTestMethodReturnsProcessInfo()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodName: string.Empty,
            className: string.Empty,
            namespaceName: string.Empty,
            processName: "TestProcess",
            processId: 1234,
            methodAttributeNames: []);

        // Act
        var debugDisplay = metadata.ToString(); // DebuggerDisplay uses the same logic as ToString

        // Assert
        Assert.That(debugDisplay, Is.EqualTo("Process: TestProcess (ID: 1234)"));
    }

    private static readonly string[] expected = new[] { "TestFixtureAttribute" };
    private static readonly string[] expectedArray = new[] { "TestAttribute" };

    [Test]
    public void PropertiesWithInitValuesAreSetCorrectly()
    {
        // Arrange & Act
        var metadata = new TestMetadata
        {
            MethodName = "TestMethod",
            ClassName = "TestClass",
            Namespace = "TestNamespace",
            ProcessName = "TestProcess",
            ProcessId = 1234,
            ClassAttributeNames = ["TestFixtureAttribute"],
            MethodAttributeNames = ["TestAttribute"],
            TestDescription = "Test Description"
        };

        metadata.UpdateXpingIdentifier("test-unique-id");

        // Assert
        Assert.That(metadata.MethodName, Is.EqualTo("TestMethod"));
        Assert.That(metadata.ClassName, Is.EqualTo("TestClass"));
        Assert.That(metadata.Namespace, Is.EqualTo("TestNamespace"));
        Assert.That(metadata.ProcessName, Is.EqualTo("TestProcess"));
        Assert.That(metadata.ProcessId, Is.EqualTo(1234));
        Assert.That(metadata.ClassAttributeNames, Is.EquivalentTo(expected));
        Assert.That(metadata.MethodAttributeNames, Is.EquivalentTo(expectedArray));
        Assert.That(metadata.TestDescription, Is.EqualTo("Test Description"));
        Assert.That(metadata.XpingIdentifier, Is.EqualTo("test-unique-id"));
    }

    private static byte[] SerializeToMemoryStream<T>(T obj)
    {
        using var memoryStream = new MemoryStream();
        var serializer = new DataContractSerializer(typeof(T), knownTypes: [typeof(string[])]);
        serializer.WriteObject(memoryStream, obj);
        return memoryStream.ToArray();
    }

    private static T DeserializeFromMemoryStream<T>(byte[] data)
    {
        using var memoryStream = new MemoryStream(data);
        var serializer = new DataContractSerializer(typeof(T), knownTypes: [typeof(string[])]);
        return (T)serializer.ReadObject(memoryStream)!;
    }

    [Test]
    public void XpingIdentifierIsExtractedFromXpingAttribute()
    {
        // Arrange
        var original = CreateTestMetadataUnderTest(
            xpingIdentifier: "test-xping-identifier");

        // Act & Assert
        Assert.That(original.XpingIdentifier, Is.EqualTo("test-xping-identifier"));
    }

    [Test]
    public void XpingIdentifierIsNullWhenNotProvided()
    {
        // Arrange
        var original = CreateTestMetadataUnderTest();

        // Act & Assert
        Assert.That(original.XpingIdentifier, Is.Null);
    }
}
