/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Serialization;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Provides centralized JSON serialization configuration for the Xping SDK.
/// This class defines standard serialization options used across different components
/// to ensure consistency in how test execution data is serialized.
/// </summary>
public static class XpingSerializerOptions
{
    /// <summary>
    /// Gets the default JSON serialization options for API communication.
    /// Uses camel case naming, compact formatting, and string enum conversion.
    /// </summary>
    /// <remarks>
    /// Configuration:
    /// - PropertyNamingPolicy: CamelCase (e.g., "testName" instead of "TestName")
    /// - WriteIndented: false (compact JSON for efficient transmission)
    /// - DefaultIgnoreCondition: WhenWritingNull (reduces payload size)
    /// - PropertyNameCaseInsensitive: true (tolerant deserialization)
    /// - Converters: JsonStringEnumConverter (enums as strings for readability)
    /// </remarks>
    public static JsonSerializerOptions ApiOptions { get; } = CreateApiOptions();

    /// <summary>
    /// Gets JSON serialization options optimized for file persistence.
    /// Similar to API options but may include indentation for debugging.
    /// </summary>
    /// <remarks>
    /// Configuration:
    /// - PropertyNamingPolicy: CamelCase
    /// - WriteIndented: false (compact to save disk space)
    /// - DefaultIgnoreCondition: WhenWritingNull
    /// - PropertyNameCaseInsensitive: true
    /// - Converters: JsonStringEnumConverter
    /// </remarks>
    public static JsonSerializerOptions FileOptions { get; } = CreateFileOptions();

    /// <summary>
    /// Gets JSON serialization options for testing purposes.
    /// Includes indentation for better readability in test output.
    /// </summary>
    /// <remarks>
    /// Configuration:
    /// - PropertyNamingPolicy: CamelCase
    /// - WriteIndented: true (human-readable for debugging)
    /// - DefaultIgnoreCondition: WhenWritingNull
    /// - PropertyNameCaseInsensitive: true
    /// - Converters: JsonStringEnumConverter
    /// </remarks>
    public static JsonSerializerOptions TestOptions { get; } = CreateTestOptions();

    private static JsonSerializerOptions CreateApiOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        return options;
    }

    private static JsonSerializerOptions CreateFileOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        return options;
    }

    private static JsonSerializerOptions CreateTestOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        return options;
    }
}
