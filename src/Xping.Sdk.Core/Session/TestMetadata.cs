/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Diagnostics;
using System.Runtime.Serialization;

namespace Xping.Sdk.Core.Session;

/// <summary>
/// Represents metadata information for a test method and its containing class.
/// Contains information about method names, class details, and their attributes.
/// </summary>
[Serializable]
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public sealed class TestMetadata : ISerializable, IDeserializationCallback, IEquatable<TestMetadata>
{
    /// <summary>
    /// Gets or sets the name of the test method.
    /// </summary>
    public string MethodName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the class containing the test method.
    /// </summary>
    public string ClassName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the namespace of the class containing the test method.
    /// </summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the process executing the test.
    /// <para>
    /// Note: On macOS and Linux, this value may be truncated to the first 15 characters due to 
    /// OS-level limitations in the process table.
    /// </para>
    /// </summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the process ID of the executing process.
    /// </summary>
    public int ProcessId { get; init; }

    /// <summary>
    /// Gets or sets the list of attribute names applied to the class.
    /// Note: Stores attribute names instead of Attribute objects for serialization compatibility.
    /// </summary>
    public IReadOnlyCollection<string> ClassAttributeNames { get; init; } = [];

    /// <summary>
    /// Gets or sets the list of attribute names applied to the test method.
    /// Note: Stores attribute names instead of Attribute objects for serialization compatibility.
    /// </summary>
    public IReadOnlyCollection<string> MethodAttributeNames { get; init; } = [];

    /// <summary>
    /// Gets or sets the test description extracted from test attributes.
    /// This is stored directly to avoid reflection during deserialization.
    /// </summary>
    public string? TestDescription { get; init; }

    /// <summary>
    /// Gets or sets the geographic location information where the test is being executed.
    /// Contains details like country, region, city, timezone, and IP-based location data.
    /// </summary>
    public TestLocation? Location { get; init; }

    /// <summary>
    /// Gets the full qualified name of the test method including namespace and class name.
    /// </summary>
    public string FullyQualifiedName => $"{Namespace}.{ClassName}.{MethodName}";

    /// <summary>
    /// Gets the display name for the test, preferring the description if available.
    /// </summary>
    public string DisplayName => !string.IsNullOrEmpty(TestDescription) ? TestDescription : MethodName;

    /// <summary>
    /// Gets a value indicating whether this metadata represents a test method (as opposed to a process fallback).
    /// </summary>
    public bool IsTestMethod => !string.IsNullOrEmpty(MethodName) &&
                                !string.IsNullOrEmpty(ClassName) &&
                                !string.IsNullOrEmpty(Namespace) &&
                                MethodAttributeNames.Count != 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestMetadata"/> class.
    /// </summary>
    public TestMetadata()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestMetadata"/> class with serialized data.
    /// </summary>
    /// <param name="info">The serialization info.</param>
    /// <param name="context">The streaming context.</param>
    public TestMetadata(SerializationInfo info, StreamingContext context)
    {
        ArgumentNullException.ThrowIfNull(info, nameof(info));

        MethodName = info.GetString(nameof(MethodName)) ?? string.Empty;
        ClassName = info.GetString(nameof(ClassName)) ?? string.Empty;
        Namespace = info.GetString(nameof(Namespace)) ?? string.Empty;
        ProcessName = info.GetString(nameof(ProcessName)) ?? string.Empty;
        ProcessId = info.GetInt32(nameof(ProcessId));
        ClassAttributeNames =
            (info.GetValue(nameof(ClassAttributeNames), typeof(string[])) as string[])?.ToList() ?? [];
        MethodAttributeNames =
            (info.GetValue(nameof(MethodAttributeNames), typeof(string[])) as string[])?.ToList() ?? [];
        TestDescription = info.GetString(nameof(TestDescription));
        Location = info.GetValue(nameof(Location), typeof(TestLocation)) as TestLocation;
    }

    /// <summary>
    /// Checks if a specific attribute type name exists either on the method or the class level.
    /// </summary>
    /// <param name="attributeName">The name of the attribute to check for (e.g., "TestAttribute").</param>
    /// <returns>True if the attribute exists on either the method or class level; otherwise, false.</returns>
    public bool HasAttribute(string attributeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);

        return MethodAttributeNames.Any(name => name.Contains(attributeName, StringComparison.OrdinalIgnoreCase)) ||
               ClassAttributeNames.Any(name => name.Contains(attributeName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a test attribute exists on the method level.
    /// </summary>
    /// <returns>True if a test attribute exists; otherwise, false.</returns>
    public bool HasTestAttribute()
    {
        return MethodAttributeNames.Any(name =>
            name.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Fact", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Theory", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns a string representation of the test metadata.
    /// </summary>
    /// <returns>A formatted string containing the test information.</returns>
    public override string ToString()
    {
        if (IsTestMethod)
        {
            var description = !string.IsNullOrEmpty(TestDescription) ? $" - {TestDescription}" : "";
            var location = Location != null ? $" [{Location.Country}/{Location.Region}]" : "";
            return $"{FullyQualifiedName}{description}{location} [{ProcessName}:{ProcessId}]";
        }

        var locationInfo = Location != null ? $" [{Location.Country}/{Location.Region}]" : "";
        return $"Process: {ProcessName} (ID: {ProcessId}){locationInfo}";
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>True if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as TestMetadata);
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>True if the current object is equal to the other parameter; otherwise, false.</returns>
    public bool Equals(TestMetadata? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return MethodName == other.MethodName &&
               ClassName == other.ClassName &&
               Namespace == other.Namespace &&
               ProcessName == other.ProcessName &&
               ProcessId == other.ProcessId;
    }

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(MethodName, ClassName, Namespace, ProcessName, ProcessId);
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(TestMetadata? left, TestMetadata? right)
    {
        return Equals(left, right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(TestMetadata? left, TestMetadata? right)
    {
        return !Equals(left, right);
    }

    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue(nameof(MethodName), MethodName);
        info.AddValue(nameof(ClassName), ClassName);
        info.AddValue(nameof(Namespace), Namespace);
        info.AddValue(nameof(ProcessName), ProcessName);
        info.AddValue(nameof(ProcessId), ProcessId);
        info.AddValue(nameof(ClassAttributeNames), ClassAttributeNames.ToArray());
        info.AddValue(nameof(MethodAttributeNames), MethodAttributeNames.ToArray());
        info.AddValue(nameof(TestDescription), TestDescription);
        info.AddValue(nameof(Location), Location);
    }

    void IDeserializationCallback.OnDeserialization(object? sender)
    {
        // Validation after deserialization if needed
    }

    private string GetDebuggerDisplay()
    {
        return IsTestMethod
            ? $"{FullyQualifiedName} [{ProcessName}:{ProcessId}]"
            : $"Process: {ProcessName} (ID: {ProcessId})";
    }
}