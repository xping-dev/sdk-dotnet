/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Xping.Sdk.Core.Session;

namespace Xping.Sdk.UnitTests.Session;

[TestFixture]
public sealed class TestMetadataTests
{
    private static TestMetadata CreateTestMetadataUnderTest(
        string methodName = "TestMethod",
        string className = "TestClass",
        string namespaceName = "TestNamespace",
        string processName = "testprocess",
        int processId = 1234,
        IReadOnlyCollection<string>? classAttributeNames = null,
        IReadOnlyCollection<string>? methodAttributeNames = null,
        string? testDescription = null)
    {
        return new TestMetadata
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
    }

    [Test]
    public void Constructor_DefaultConstructor_InitializesWithEmptyValues()
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
    }

    [Test]
    public void FullyQualifiedName_WithValidNamespaceClassAndMethod_ReturnsCorrectFormat()
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
    public void DisplayName_WithTestDescription_ReturnsDescription()
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
    public void DisplayName_WithoutTestDescription_ReturnsMethodName()
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
    public void DisplayName_WithEmptyTestDescription_ReturnsMethodName()
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
    public void IsTestMethod_WithValidTestData_ReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest();

        // Act
        var isTestMethod = metadata.IsTestMethod;

        // Assert
        Assert.That(isTestMethod, Is.True);
    }

    [Test]
    public void IsTestMethod_WithEmptyMethodName_ReturnsFalse()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(methodName: string.Empty);

        // Act
        var isTestMethod = metadata.IsTestMethod;

        // Assert
        Assert.That(isTestMethod, Is.False);
    }

    [Test]
    public void IsTestMethod_WithEmptyClassName_ReturnsFalse()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(className: string.Empty);

        // Act
        var isTestMethod = metadata.IsTestMethod;

        // Assert
        Assert.That(isTestMethod, Is.False);
    }

    [Test]
    public void IsTestMethod_WithEmptyNamespace_ReturnsFalse()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(namespaceName: string.Empty);

        // Act
        var isTestMethod = metadata.IsTestMethod;

        // Assert
        Assert.That(isTestMethod, Is.False);
    }

    [Test]
    public void IsTestMethod_WithEmptyMethodAttributes_ReturnsFalse()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(methodAttributeNames: []);

        // Act
        var isTestMethod = metadata.IsTestMethod;

        // Assert
        Assert.That(isTestMethod, Is.False);
    }

    [Test]
    public void HasAttribute_WithExistingMethodAttribute_ReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["TestAttribute", "CategoryAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasAttribute("TestAttribute"), Is.True);
        Assert.That(metadata.HasAttribute("CategoryAttribute"), Is.True);
    }

    [Test]
    public void HasAttribute_WithExistingClassAttribute_ReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            classAttributeNames: ["TestFixtureAttribute", "CategoryAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasAttribute("TestFixtureAttribute"), Is.True);
        Assert.That(metadata.HasAttribute("CategoryAttribute"), Is.True);
    }

    [Test]
    public void HasAttribute_WithNonExistingAttribute_ReturnsFalse()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["TestAttribute"],
            classAttributeNames: ["TestFixtureAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasAttribute("NonExistingAttribute"), Is.False);
    }

    [Test]
    public void HasAttribute_WithPartialAttributeName_ReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["TestAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasAttribute("Test"), Is.True);
    }

    [Test]
    public void HasAttribute_WithCaseInsensitiveMatch_ReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["TestAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasAttribute("testattribute"), Is.True);
        Assert.That(metadata.HasAttribute("TESTATTRIBUTE"), Is.True);
    }

    [Test]
    public void HasAttribute_WithNullOrWhitespaceAttributeName_ThrowsArgumentException()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => metadata.HasAttribute(null!));
        Assert.Throws<ArgumentException>(() => metadata.HasAttribute(string.Empty));
        Assert.Throws<ArgumentException>(() => metadata.HasAttribute("   "));
    }

    [Test]
    public void HasTestAttribute_WithTestAttribute_ReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["TestAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasTestAttribute(), Is.True);
    }

    [Test]
    public void HasTestAttribute_WithFactAttribute_ReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["FactAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasTestAttribute(), Is.True);
    }

    [Test]
    public void HasTestAttribute_WithTheoryAttribute_ReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["TheoryAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasTestAttribute(), Is.True);
    }

    [Test]
    public void HasTestAttribute_WithNonTestAttribute_ReturnsFalse()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodAttributeNames: ["CategoryAttribute", "ObsoleteAttribute"]);

        // Act & Assert
        Assert.That(metadata.HasTestAttribute(), Is.False);
    }

    [Test]
    public void ToString_ForTestMethod_ReturnsFormattedString()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "testprocess",
            processId: 1234);

        // Act
        var result = metadata.ToString();

        // Assert
        Assert.That(result, Is.EqualTo("TestNamespace.TestClass.TestMethod [testprocess:1234]"));
    }

    [Test]
    public void ToString_ForTestMethodWithDescription_ReturnsFormattedStringWithDescription()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "testprocess",
            processId: 1234,
            testDescription: "Test Description");

        // Act
        var result = metadata.ToString();

        // Assert
        Assert.That(result, Is.EqualTo("TestNamespace.TestClass.TestMethod - Test Description [testprocess:1234]"));
    }

    [Test]
    public void ToString_ForNonTestMethod_ReturnsProcessInfo()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodName: string.Empty,
            className: string.Empty,
            namespaceName: string.Empty,
            processName: "testprocess",
            processId: 1234,
            methodAttributeNames: []);

        // Act
        var result = metadata.ToString();

        // Assert
        Assert.That(result, Is.EqualTo("Process: testprocess (ID: 1234)"));
    }

    [Test]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var metadata1 = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "testprocess",
            processId: 1234);

        var metadata2 = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "testprocess",
            processId: 1234);

        // Act & Assert
        Assert.That(metadata1.Equals(metadata2), Is.True);
        Assert.That(metadata1 == metadata2, Is.True);
        Assert.That(metadata1 != metadata2, Is.False);
    }

    [Test]
    public void Equals_WithDifferentValues_ReturnsFalse()
    {
        // Arrange
        var metadata1 = CreateTestMetadataUnderTest(
            methodName: "TestMethod1",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "testprocess",
            processId: 1234);

        var metadata2 = CreateTestMetadataUnderTest(
            methodName: "TestMethod2",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "testprocess",
            processId: 1234);

        // Act & Assert
        Assert.That(metadata1.Equals(metadata2), Is.False);
        Assert.That(metadata1 == metadata2, Is.False);
        Assert.That(metadata1 != metadata2, Is.True);
    }

    [Test]
    public void Equals_WithNull_ReturnsFalse()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest();

        // Act & Assert
        Assert.That(metadata.Equals(null), Is.False);
        Assert.That(metadata == null, Is.False);
        Assert.That(metadata != null, Is.True);
    }

    [Test]
    public void Equals_WithSameReference_ReturnsTrue()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest();

        // Act & Assert
        Assert.That(metadata.Equals(metadata), Is.True);
        Assert.That(metadata == metadata, Is.True);
        Assert.That(metadata != metadata, Is.False);
    }

    [Test]
    public void GetHashCode_WithSameValues_ReturnsSameHashCode()
    {
        // Arrange
        var metadata1 = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "testprocess",
            processId: 1234);

        var metadata2 = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "testprocess",
            processId: 1234);

        // Act & Assert
        Assert.That(metadata1.GetHashCode(), Is.EqualTo(metadata2.GetHashCode()));
    }

    [Test]
    public void GetHashCode_WithDifferentValues_ReturnsDifferentHashCode()
    {
        // Arrange
        var metadata1 = CreateTestMetadataUnderTest(
            methodName: "TestMethod1",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "testprocess",
            processId: 1234);

        var metadata2 = CreateTestMetadataUnderTest(
            methodName: "TestMethod2",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "testprocess",
            processId: 1234);

        // Act & Assert
        Assert.That(metadata1.GetHashCode(), Is.Not.EqualTo(metadata2.GetHashCode()));
    }

    [Test]
    public void Serialization_RoundTrip_PreservesAllData()
    {
        // Arrange
        var original = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "testprocess",
            processId: 1234,
            classAttributeNames: ["TestFixtureAttribute", "CategoryAttribute"],
            methodAttributeNames: ["TestAttribute", "DescriptionAttribute"],
            testDescription: "Test Description");

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
    }

    [Test]
    public void Serialization_WithNullValues_HandlesNullsCorrectly()
    {
        // Arrange
        var original = new TestMetadata
        {
            MethodName = "TestMethod",
            ClassName = "TestClass",
            Namespace = "TestNamespace",
            ProcessName = "testprocess",
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
    public void SerializationConstructor_WithNullSerializationInfo_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new TestMetadata(null!, new StreamingContext()));
    }

    [Test]
    public void DebuggerDisplay_ForTestMethod_ReturnsCorrectFormat()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodName: "TestMethod",
            className: "TestClass",
            namespaceName: "TestNamespace",
            processName: "testprocess",
            processId: 1234);

        // Act
        var debugDisplay = metadata.ToString(); // DebuggerDisplay uses the same logic as ToString

        // Assert
        Assert.That(debugDisplay, Is.EqualTo("TestNamespace.TestClass.TestMethod [testprocess:1234]"));
    }

    [Test]
    public void DebuggerDisplay_ForNonTestMethod_ReturnsProcessInfo()
    {
        // Arrange
        var metadata = CreateTestMetadataUnderTest(
            methodName: string.Empty,
            className: string.Empty,
            namespaceName: string.Empty,
            processName: "testprocess",
            processId: 1234,
            methodAttributeNames: []);

        // Act
        var debugDisplay = metadata.ToString(); // DebuggerDisplay uses the same logic as ToString

        // Assert
        Assert.That(debugDisplay, Is.EqualTo("Process: testprocess (ID: 1234)"));
    }

    [Test]
    public void Properties_WithInitValues_AreSetCorrectly()
    {
        // Arrange & Act
        var metadata = new TestMetadata
        {
            MethodName = "TestMethod",
            ClassName = "TestClass",
            Namespace = "TestNamespace",
            ProcessName = "testprocess",
            ProcessId = 1234,
            ClassAttributeNames = ["TestFixtureAttribute"],
            MethodAttributeNames = ["TestAttribute"],
            TestDescription = "Test Description"
        };

        // Assert
        Assert.That(metadata.MethodName, Is.EqualTo("TestMethod"));
        Assert.That(metadata.ClassName, Is.EqualTo("TestClass"));
        Assert.That(metadata.Namespace, Is.EqualTo("TestNamespace"));
        Assert.That(metadata.ProcessName, Is.EqualTo("testprocess"));
        Assert.That(metadata.ProcessId, Is.EqualTo(1234));
        Assert.That(metadata.ClassAttributeNames, Is.EquivalentTo(new[] { "TestFixtureAttribute" }));
        Assert.That(metadata.MethodAttributeNames, Is.EquivalentTo(new[] { "TestAttribute" }));
        Assert.That(metadata.TestDescription, Is.EqualTo("Test Description"));
    }

    private static byte[] SerializeToMemoryStream<T>(T obj) where T : ISerializable
    {
        using var memoryStream = new MemoryStream();
        var formatter = new BinaryFormatter();
        formatter.Serialize(memoryStream, obj);
        return memoryStream.ToArray();
    }

    private static T DeserializeFromMemoryStream<T>(byte[] data)
    {
        using var memoryStream = new MemoryStream(data);
        var formatter = new BinaryFormatter();
        return (T)formatter.Deserialize(memoryStream);
    }
}
