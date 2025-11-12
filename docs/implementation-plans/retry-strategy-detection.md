# Retry Strategy Detection - Implementation Plan

**Feature:** Retry Strategy Detection for Test Execution  
**Target Frameworks:** XUnit, NUnit, MSTest  
**Status:** Planning Phase  
**Created:** November 12, 2025  
**Owner:** Xping SDK Team

---

## Executive Summary

This implementation plan outlines the design and development of **Retry Strategy Detection** functionality for the Xping SDK. This feature will enable detection and tracking of test retry behavior across all three supported testing frameworks (XUnit, NUnit, MSTest), providing insights into flaky tests, retry patterns, and test reliability.

### Goals

1. **Detect Retry Attributes**: Automatically identify when tests are configured with retry attributes
2. **Track Retry Attempts**: Record detailed metadata about each retry attempt
3. **Analyze Retry Patterns**: Enable identification of tests that consistently pass on Nth retry
4. **Surface Flakiness**: Unmask hidden flakiness that retries are concealing
5. **Framework Agnostic**: Provide consistent retry detection across XUnit, NUnit, and MSTest

### Success Criteria

- ✅ Retry metadata captured for all three frameworks
- ✅ Accurate attempt numbering (1 = first run, 2+ = retry)
- ✅ Detection of both native and custom retry attributes
- ✅ Zero performance impact on test execution
- ✅ Backward compatible with existing SDK usage
- ✅ Comprehensive test coverage (>85%)

---

## Architecture Overview

### Core Components

```
Xping.Sdk.Core/
├── Models/
│   ├── RetryMetadata.cs          (NEW)
│   └── TestExecution.cs          (MODIFIED)
├── Retry/                        (NEW)
│   ├── IRetryDetector.cs
│   └── RetryAttributeRegistry.cs
└── Diagnostics/
    └── XpingDiagnostics.cs       (MODIFIED - add retry logging)

Xping.Sdk.XUnit/
├── Retry/                        (NEW)
│   ├── XUnitRetryDetector.cs
│   └── XUnitRetryAttributeHelper.cs
└── XpingMessageSink.cs           (MODIFIED)

Xping.Sdk.NUnit/
├── Retry/                        (NEW)
│   ├── NUnitRetryDetector.cs
│   └── NUnitRetryAttributeHelper.cs
└── XpingTrackAttribute.cs        (MODIFIED)

Xping.Sdk.MSTest/
├── Retry/                        (NEW)
│   ├── MSTestRetryDetector.cs
│   └── MSTestRetryAttributeHelper.cs
└── XpingTestBase.cs              (MODIFIED)
```

### Data Flow

```
Test Execution Start
        ↓
Framework Adapter (XUnit/NUnit/MSTest)
        ↓
Retry Detector (Framework-Specific)
        ↓
RetryMetadata Creation (if retry detected)
        ↓
TestExecution with Retry Info
        ↓
ExecutionTracker Collection
        ↓
Upload to Xping Platform
```

---

## Phase 1: Core Domain Model (Week 1, Days 1-2)

### 1.1 Create RetryMetadata Model

**Location:** `src/Xping.Sdk.Core/Models/RetryMetadata.cs`

**Implementation:**

```csharp
/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models;

using System;

/// <summary>
/// Represents metadata about test retry behavior and configuration.
/// </summary>
/// <remarks>
/// This metadata helps identify flaky tests that pass only after retry,
/// tests with timing issues that consistently pass on the Nth retry,
/// and patterns indicating test reliability problems.
/// </remarks>
public sealed class RetryMetadata
{
    /// <summary>
    /// Gets or sets the current attempt number for this test execution.
    /// </summary>
    /// <remarks>
    /// 1 = first run (no retry), 2 = first retry, 3 = second retry, etc.
    /// This enables analysis of "pass on Nth attempt" patterns.
    /// </remarks>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum number of retries configured for this test.
    /// </summary>
    /// <remarks>
    /// This is the total number of retry attempts allowed, not including the initial attempt.
    /// For example, MaxRetries = 3 means: 1 initial attempt + 3 retry attempts = 4 total attempts.
    /// </remarks>
    public int MaxRetries { get; set; }

    /// <summary>
    /// Gets or sets whether this test passed after a retry (not on first attempt).
    /// </summary>
    /// <remarks>
    /// False = passed on first attempt (no retry needed)
    /// True = passed after one or more retries (indicates flakiness)
    /// </remarks>
    public bool PassedOnRetry { get; set; }

    /// <summary>
    /// Gets or sets the configured delay between retry attempts.
    /// </summary>
    /// <remarks>
    /// This is the delay configured in the retry attribute.
    /// Actual delay may vary based on framework implementation.
    /// </remarks>
    public TimeSpan DelayBetweenRetries { get; set; }

    /// <summary>
    /// Gets or sets the reason for the retry configuration.
    /// </summary>
    /// <remarks>
    /// Examples: "Timeout", "NetworkError", "DatabaseContention", "RaceCondition"
    /// This is typically extracted from custom retry attributes or attribute properties.
    /// May be null if not specified by the test author.
    /// </remarks>
    public string? RetryReason { get; set; }

    /// <summary>
    /// Gets or sets the name of the retry attribute or strategy used.
    /// </summary>
    /// <remarks>
    /// Examples: "RetryFact", "Retry", "TestRetry", "CustomRetry"
    /// Helps identify which retry mechanism is being used.
    /// </remarks>
    public string? RetryAttributeName { get; set; }

    /// <summary>
    /// Gets or sets additional retry-related metadata.
    /// </summary>
    /// <remarks>
    /// Used for framework-specific or custom retry attribute properties.
    /// Examples: retry filter types, exception types to retry on, custom conditions.
    /// </remarks>
    public Dictionary<string, string> AdditionalMetadata { get; } = new();
}
```

**Tasks:**
- [ ] Create `RetryMetadata.cs` in `src/Xping.Sdk.Core/Models/`
- [ ] Add XML documentation for all properties
- [ ] Add unit tests for serialization/deserialization
- [ ] Update namespace imports as needed

**Test Cases:**
```csharp
// RetryMetadataTests.cs
- Should_Initialize_With_Default_Values
- Should_Set_All_Properties_Correctly
- Should_Serialize_To_Json_Successfully
- Should_Deserialize_From_Json_Successfully
- Should_Handle_Null_Optional_Properties
```

---

### 1.2 Update TestExecution Model

**Location:** `src/Xping.Sdk.Core/Models/TestExecution.cs`

**Changes:**

```csharp
/// <summary>
/// Gets or sets retry metadata if the test is configured for retry.
/// </summary>
/// <remarks>
/// Null if the test does not have retry configuration.
/// Contains retry attempt information, max retries, and retry strategy details
/// when the test is executed with a retry mechanism.
/// </remarks>
public RetryMetadata? Retry { get; set; }
```

**Tasks:**
- [ ] Add `Retry` property to `TestExecution` class
- [ ] Add XML documentation
- [ ] Update existing unit tests to handle nullable `Retry` property
- [ ] Verify serialization includes `Retry` field

**Test Cases:**
```csharp
// TestExecutionTests.cs (update existing)
- Should_Serialize_With_Null_Retry_Metadata
- Should_Serialize_With_Populated_Retry_Metadata
- Should_Deserialize_Without_Retry_Metadata (backward compatibility)
```

---

## Phase 2: Core Retry Detection Infrastructure (Week 1, Days 3-5)

### 2.1 Create IRetryDetector Interface

**Location:** `src/Xping.Sdk.Core/Retry/IRetryDetector.cs`

**Implementation:**

```csharp
/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Retry;

using Xping.Sdk.Core.Models;

/// <summary>
/// Defines functionality for detecting retry attributes and metadata from test methods.
/// </summary>
/// <typeparam name="TTest">The framework-specific test representation type.</typeparam>
public interface IRetryDetector<in TTest>
{
    /// <summary>
    /// Detects retry metadata from the given test.
    /// </summary>
    /// <param name="test">The test to analyze for retry attributes.</param>
    /// <returns>
    /// RetryMetadata if retry attributes are found; otherwise, null.
    /// </returns>
    RetryMetadata? DetectRetryMetadata(TTest test);

    /// <summary>
    /// Checks if the specified test has any retry attributes.
    /// </summary>
    /// <param name="test">The test to check.</param>
    /// <returns>True if the test has retry attributes; otherwise, false.</returns>
    bool HasRetryAttribute(TTest test);

    /// <summary>
    /// Gets the current attempt number for the test execution.
    /// </summary>
    /// <param name="test">The test being executed.</param>
    /// <returns>
    /// The attempt number (1 for first attempt, 2+ for retries).
    /// Returns 1 if attempt tracking is not available.
    /// </returns>
    int GetCurrentAttemptNumber(TTest test);
}
```

**Tasks:**
- [ ] Create `src/Xping.Sdk.Core/Retry/` directory
- [ ] Create `IRetryDetector.cs` interface
- [ ] Add XML documentation
- [ ] Consider thread-safety requirements

---

### 2.2 Create RetryAttributeRegistry

**Location:** `src/Xping.Sdk.Core/Retry/RetryAttributeRegistry.cs`

**Purpose:** Central registry for known retry attribute types across frameworks

**Implementation:**

```csharp
/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Retry;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Registry of known retry attribute names across different test frameworks.
/// </summary>
/// <remarks>
/// Maintains a list of common retry attribute names to help identify
/// retry mechanisms even when reflection-based detection isn't available.
/// </remarks>
public static class RetryAttributeRegistry
{
    /// <summary>
    /// Known retry attribute names for xUnit.
    /// </summary>
    public static readonly HashSet<string> XUnitRetryAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "RetryFact",
        "RetryTheory",
        "RetryAttribute",
        "XunitRetryAttribute",
    };

    /// <summary>
    /// Known retry attribute names for NUnit.
    /// </summary>
    public static readonly HashSet<string> NUnitRetryAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Retry",
        "RetryAttribute",
        "NUnit.Framework.RetryAttribute",
    };

    /// <summary>
    /// Known retry attribute names for MSTest.
    /// </summary>
    public static readonly HashSet<string> MSTestRetryAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TestRetry",
        "RetryAttribute",
        "DataTestMethodWithRetry",
    };

    /// <summary>
    /// Checks if the given attribute name is a known retry attribute for any framework.
    /// </summary>
    public static bool IsKnownRetryAttribute(string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return false;
        }

        return XUnitRetryAttributes.Contains(attributeName) ||
               NUnitRetryAttributes.Contains(attributeName) ||
               MSTestRetryAttributes.Contains(attributeName);
    }

    /// <summary>
    /// Registers a custom retry attribute name for a specific framework.
    /// </summary>
    public static void RegisterCustomRetryAttribute(string framework, string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return;
        }

        var registry = framework?.ToUpperInvariant() switch
        {
            "XUNIT" => XUnitRetryAttributes,
            "NUNIT" => NUnitRetryAttributes,
            "MSTEST" => MSTestRetryAttributes,
            _ => null
        };

        registry?.Add(attributeName);
    }
}
```

**Tasks:**
- [ ] Create `RetryAttributeRegistry.cs`
- [ ] Add support for custom retry attributes
- [ ] Add unit tests
- [ ] Document extensibility pattern

**Test Cases:**
```csharp
// RetryAttributeRegistryTests.cs
- Should_Recognize_All_XUnit_Retry_Attributes
- Should_Recognize_All_NUnit_Retry_Attributes
- Should_Recognize_All_MSTest_Retry_Attributes
- Should_Register_Custom_Retry_Attributes
- Should_Be_Case_Insensitive
```

---

## Phase 3: XUnit Retry Detection (Week 2, Days 1-2)

### 3.1 Create XUnitRetryDetector

**Location:** `src/Xping.Sdk.XUnit/Retry/XUnitRetryDetector.cs`

**Implementation:**

```csharp
/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit.Retry;

using System;
using System.Linq;
using System.Reflection;
using Xunit.Abstractions;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Retry;

/// <summary>
/// Detects retry attributes and metadata for xUnit tests.
/// </summary>
public sealed class XUnitRetryDetector : IRetryDetector<ITest>
{
    /// <inheritdoc/>
    public RetryMetadata? DetectRetryMetadata(ITest test)
    {
        if (test?.TestCase?.TestMethod?.Method == null)
        {
            return null;
        }

        var method = test.TestCase.TestMethod.Method;
        
        // Try to get reflection-based method info
        var methodInfo = GetMethodInfo(method);
        if (methodInfo == null)
        {
            return null;
        }

        // Look for retry attributes
        var retryAttribute = FindRetryAttribute(methodInfo);
        if (retryAttribute == null)
        {
            return null;
        }

        return ExtractRetryMetadata(retryAttribute, test);
    }

    /// <inheritdoc/>
    public bool HasRetryAttribute(ITest test)
    {
        return DetectRetryMetadata(test) != null;
    }

    /// <inheritdoc/>
    public int GetCurrentAttemptNumber(ITest test)
    {
        // xUnit doesn't expose retry attempt count directly in ITest
        // We need to look for custom properties or trait data
        
        if (test?.TestCase == null)
        {
            return 1;
        }

        // Check for retry attempt in traits or test case properties
        var traits = test.TestCase.Traits;
        if (traits.ContainsKey("RetryAttempt"))
        {
            var attemptValues = traits["RetryAttempt"];
            if (attemptValues.Any() && int.TryParse(attemptValues.First(), out var attempt))
            {
                return attempt;
            }
        }

        // Default to first attempt
        return 1;
    }

    private static MethodInfo? GetMethodInfo(IMethodInfo method)
    {
        try
        {
            // xUnit's IMethodInfo can be converted to reflection MethodInfo
            var type = Type.GetType(method.Type.Name);
            if (type == null)
            {
                return null;
            }

            var methodInfo = type.GetMethod(
                method.Name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            return methodInfo;
        }
        catch
        {
            return null;
        }
    }

    private static Attribute? FindRetryAttribute(MethodInfo methodInfo)
    {
        // Get all attributes
        var attributes = methodInfo.GetCustomAttributes(true);

        // Look for known retry attributes
        foreach (var attr in attributes)
        {
            var attrType = attr.GetType();
            var attrName = attrType.Name.Replace("Attribute", "");

            if (RetryAttributeRegistry.XUnitRetryAttributes.Contains(attrName))
            {
                return attr as Attribute;
            }
        }

        return null;
    }

    private static RetryMetadata ExtractRetryMetadata(Attribute retryAttribute, ITest test)
    {
        var attrType = retryAttribute.GetType();
        var metadata = new RetryMetadata
        {
            RetryAttributeName = attrType.Name.Replace("Attribute", ""),
            AttemptNumber = 1, // Will be updated during execution
        };

        // Extract MaxRetries property (common in retry attributes)
        var maxRetriesProperty = attrType.GetProperty("MaxRetries");
        if (maxRetriesProperty != null)
        {
            var value = maxRetriesProperty.GetValue(retryAttribute);
            if (value is int maxRetries)
            {
                metadata.MaxRetries = maxRetries;
            }
        }

        // Extract Delay property if available
        var delayProperty = attrType.GetProperty("DelayMilliseconds") 
                           ?? attrType.GetProperty("Delay");
        if (delayProperty != null)
        {
            var value = delayProperty.GetValue(retryAttribute);
            if (value is int delayMs)
            {
                metadata.DelayBetweenRetries = TimeSpan.FromMilliseconds(delayMs);
            }
        }

        // Get current attempt number
        metadata.AttemptNumber = GetAttemptNumberFromTest(test);

        // Determine if passed on retry
        metadata.PassedOnRetry = metadata.AttemptNumber > 1 
                                  && test.TestCase != null;

        return metadata;
    }

    private static int GetAttemptNumberFromTest(ITest test)
    {
        // Try to extract from display name (some retry libraries append attempt number)
        var displayName = test.DisplayName;
        if (displayName.Contains("(attempt"))
        {
            var attemptStr = displayName.Split(new[] { "(attempt", ")" }, StringSplitOptions.RemoveEmptyEntries)
                                       .LastOrDefault()?.Trim();
            if (int.TryParse(attemptStr, out var attempt))
            {
                return attempt;
            }
        }

        return 1;
    }
}
```

**Tasks:**
- [ ] Create `src/Xping.Sdk.XUnit/Retry/` directory
- [ ] Implement `XUnitRetryDetector`
- [ ] Handle xUnit-specific retry libraries (xunit.extensions.retry, etc.)
- [ ] Add comprehensive unit tests
- [ ] Test with actual retry scenarios

**Test Cases:**
```csharp
// XUnitRetryDetectorTests.cs
- Should_Detect_RetryFact_Attribute
- Should_Extract_MaxRetries_Property
- Should_Extract_Delay_Property
- Should_Return_Null_For_Non_Retry_Test
- Should_Detect_Custom_Retry_Attributes
- Should_Get_Current_Attempt_Number
- Should_Handle_Missing_Properties_Gracefully
```

---

### 3.2 Update XpingMessageSink

**Location:** `src/Xping.Sdk.XUnit/XpingMessageSink.cs`

**Changes:**

1. Add field for retry detector:
```csharp
private readonly XUnitRetryDetector _retryDetector;
```

2. Initialize in constructor:
```csharp
public XpingMessageSink(IMessageSink innerSink)
{
    // ... existing code ...
    _retryDetector = new XUnitRetryDetector();
}
```

3. Update `RecordTestExecution` method to detect and include retry metadata:
```csharp
private static void RecordTestExecution(
    ITest test,
    TestOutcome outcome,
    DateTime startTime,
    DateTime endTime,
    TimeSpan duration,
    decimal executionTime,
    string output,
    string? exceptionType,
    string? errorMessage,
    string? stackTrace,
    string collectionName,
    ExecutionTracker executionTracker,
    XUnitRetryDetector retryDetector)  // NEW parameter
{
    // ... existing code to create TestExecution ...

    // Detect retry metadata
    var retryMetadata = retryDetector.DetectRetryMetadata(test);
    
    var testExecution = new TestExecution
    {
        // ... existing properties ...
        Retry = retryMetadata,  // NEW property
    };

    // ... rest of existing code ...
}
```

4. Update all calls to `RecordTestExecution` to pass `_retryDetector`

**Tasks:**
- [ ] Add `_retryDetector` field to `XpingMessageSink`
- [ ] Update `RecordTestExecution` method signature
- [ ] Update all `RecordTestExecution` call sites
- [ ] Add integration tests with retry scenarios
- [ ] Test with xunit.extensions.retry package

---

## Phase 4: NUnit Retry Detection (Week 2, Days 3-4)

### 4.1 Create NUnitRetryDetector

**Location:** `src/Xping.Sdk.NUnit/Retry/NUnitRetryDetector.cs`

**Implementation:**

```csharp
/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit.Retry;

using System;
using System.Linq;
using global::NUnit.Framework;
using global::NUnit.Framework.Interfaces;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Retry;

/// <summary>
/// Detects retry attributes and metadata for NUnit tests.
/// </summary>
public sealed class NUnitRetryDetector : IRetryDetector<ITest>
{
    /// <inheritdoc/>
    public RetryMetadata? DetectRetryMetadata(ITest test)
    {
        if (test?.Method == null)
        {
            return null;
        }

        // NUnit exposes custom attributes through ITest.Method
        var retryAttribute = test.Method.GetCustomAttributes<RetryAttribute>(true)
            .FirstOrDefault();

        if (retryAttribute == null)
        {
            return null;
        }

        return ExtractRetryMetadata(retryAttribute, test);
    }

    /// <inheritdoc/>
    public bool HasRetryAttribute(ITest test)
    {
        if (test?.Method == null)
        {
            return false;
        }

        return test.Method.GetCustomAttributes<RetryAttribute>(true).Any();
    }

    /// <inheritdoc/>
    public int GetCurrentAttemptNumber(ITest test)
    {
        // NUnit provides CurrentContext.CurrentRepeatCount for retry attempts
        // CurrentRepeatCount is 0-based, so we add 1 to get attempt number
        var repeatCount = TestContext.CurrentContext.CurrentRepeatCount;
        return repeatCount + 1;
    }

    private RetryMetadata ExtractRetryMetadata(RetryAttribute retryAttribute, ITest test)
    {
        var attemptNumber = GetCurrentAttemptNumber(test);
        
        var metadata = new RetryMetadata
        {
            RetryAttributeName = "Retry",
            MaxRetries = retryAttribute.TryCount - 1, // TryCount includes initial attempt
            AttemptNumber = attemptNumber,
            PassedOnRetry = attemptNumber > 1,
        };

        // NUnit's RetryAttribute doesn't expose delay directly
        // But we can check test properties for custom metadata
        if (test.Properties.ContainsKey("RetryDelay"))
        {
            var delayValue = test.Properties.Get("RetryDelay");
            if (delayValue is int delayMs)
            {
                metadata.DelayBetweenRetries = TimeSpan.FromMilliseconds(delayMs);
            }
        }

        // Check for custom retry reason in properties
        if (test.Properties.ContainsKey("RetryReason"))
        {
            var reasonValue = test.Properties.Get("RetryReason");
            if (reasonValue is string reason)
            {
                metadata.RetryReason = reason;
            }
        }

        return metadata;
    }
}
```

**Tasks:**
- [ ] Create `src/Xping.Sdk.NUnit/Retry/` directory
- [ ] Implement `NUnitRetryDetector`
- [ ] Handle NUnit's native `[Retry]` attribute
- [ ] Test with `TestContext.CurrentContext.CurrentRepeatCount`
- [ ] Add comprehensive unit tests

**Test Cases:**
```csharp
// NUnitRetryDetectorTests.cs
- Should_Detect_Retry_Attribute
- Should_Extract_TryCount_Property
- Should_Get_Current_Attempt_From_TestContext
- Should_Calculate_MaxRetries_Correctly
- Should_Return_Null_For_Non_Retry_Test
- Should_Handle_Retry_On_First_Attempt
- Should_Handle_Retry_On_Subsequent_Attempts
```

---

### 4.2 Update XpingTrackAttribute

**Location:** `src/Xping.Sdk.NUnit/XpingTrackAttribute.cs`

**Changes:**

1. Add field for retry detector:
```csharp
private static readonly NUnitRetryDetector RetryDetector = new();
```

2. Update `CreateTestExecution` method to include retry detection:
```csharp
private static TestExecution CreateTestExecution(
    ITest test,
    DateTime startTime,
    DateTime endTime,
    TimeSpan duration,
    string? workerId,
    string? fixtureName)
{
    // ... existing code ...

    // Detect retry metadata
    var retryMetadata = RetryDetector.DetectRetryMetadata(test);
    
    var execution = new TestExecution
    {
        // ... existing properties ...
        Retry = retryMetadata,  // NEW property
    };

    // ... rest of existing code ...
    
    return execution;
}
```

**Tasks:**
- [ ] Add `NUnitRetryDetector` field
- [ ] Update `CreateTestExecution` to detect retry metadata
- [ ] Add integration tests with `[Retry]` attribute
- [ ] Verify `CurrentRepeatCount` behavior
- [ ] Test both successful and failed retry scenarios

---

## Phase 5: MSTest Retry Detection (Week 2, Day 5)

### 5.1 Create MSTestRetryDetector

**Location:** `src/Xping.Sdk.MSTest/Retry/MSTestRetryDetector.cs`

**Implementation:**

```csharp
/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.MSTest.Retry;

using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Retry;

/// <summary>
/// Detects retry attributes and metadata for MSTest tests.
/// </summary>
/// <remarks>
/// MSTest doesn't have native retry support, but custom retry attributes
/// are commonly implemented. This detector looks for common patterns.
/// </remarks>
public sealed class MSTestRetryDetector : IRetryDetector<TestContext>
{
    /// <inheritdoc/>
    public RetryMetadata? DetectRetryMetadata(TestContext testContext)
    {
        if (testContext == null)
        {
            return null;
        }

        // Get the test method via reflection
        var methodInfo = GetTestMethod(testContext);
        if (methodInfo == null)
        {
            return null;
        }

        // Look for retry attributes
        var retryAttribute = FindRetryAttribute(methodInfo);
        if (retryAttribute == null)
        {
            return null;
        }

        return ExtractRetryMetadata(retryAttribute, testContext);
    }

    /// <inheritdoc/>
    public bool HasRetryAttribute(TestContext testContext)
    {
        return DetectRetryMetadata(testContext) != null;
    }

    /// <inheritdoc/>
    public int GetCurrentAttemptNumber(TestContext testContext)
    {
        // MSTest doesn't provide native retry attempt tracking
        // Check for custom properties set by retry implementations
        
        if (testContext?.Properties == null)
        {
            return 1;
        }

        if (testContext.Properties.Contains("RetryAttempt"))
        {
            var attemptValue = testContext.Properties["RetryAttempt"];
            if (attemptValue is int attempt)
            {
                return attempt;
            }
            
            if (attemptValue is string attemptStr && int.TryParse(attemptStr, out var parsedAttempt))
            {
                return parsedAttempt;
            }
        }

        return 1;
    }

    private static MethodInfo? GetTestMethod(TestContext testContext)
    {
        try
        {
            var fullClassName = testContext.FullyQualifiedTestClassName;
            var methodName = testContext.TestName;

            if (string.IsNullOrEmpty(fullClassName) || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            var type = Type.GetType(fullClassName);
            if (type == null)
            {
                // Try alternative approach: search loaded assemblies
                type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.FullName == fullClassName);
            }

            if (type == null)
            {
                return null;
            }

            var method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            return method;
        }
        catch
        {
            return null;
        }
    }

    private static Attribute? FindRetryAttribute(MethodInfo methodInfo)
    {
        var attributes = methodInfo.GetCustomAttributes(true);

        foreach (var attr in attributes)
        {
            var attrType = attr.GetType();
            var attrName = attrType.Name.Replace("Attribute", "");

            if (RetryAttributeRegistry.MSTestRetryAttributes.Contains(attrName))
            {
                return attr as Attribute;
            }
        }

        return null;
    }

    private RetryMetadata ExtractRetryMetadata(Attribute retryAttribute, TestContext testContext)
    {
        var attrType = retryAttribute.GetType();
        var metadata = new RetryMetadata
        {
            RetryAttributeName = attrType.Name.Replace("Attribute", ""),
            AttemptNumber = GetCurrentAttemptNumber(testContext),
        };

        // Extract MaxRetries/Count property
        var maxRetriesProperty = attrType.GetProperty("MaxRetries") 
                                ?? attrType.GetProperty("RetryCount")
                                ?? attrType.GetProperty("Count");
        if (maxRetriesProperty != null)
        {
            var value = maxRetriesProperty.GetValue(retryAttribute);
            if (value is int maxRetries)
            {
                metadata.MaxRetries = maxRetries;
            }
        }

        // Extract Delay property
        var delayProperty = attrType.GetProperty("DelayMilliseconds") 
                           ?? attrType.GetProperty("Delay");
        if (delayProperty != null)
        {
            var value = delayProperty.GetValue(retryAttribute);
            if (value is int delayMs)
            {
                metadata.DelayBetweenRetries = TimeSpan.FromMilliseconds(delayMs);
            }
        }

        // Extract Reason property
        var reasonProperty = attrType.GetProperty("Reason");
        if (reasonProperty != null)
        {
            var value = reasonProperty.GetValue(retryAttribute);
            if (value is string reason)
            {
                metadata.RetryReason = reason;
            }
        }

        metadata.PassedOnRetry = metadata.AttemptNumber > 1;

        return metadata;
    }
}
```

**Tasks:**
- [ ] Create `src/Xping.Sdk.MSTest/Retry/` directory
- [ ] Implement `MSTestRetryDetector`
- [ ] Test with common MSTest retry packages (MSTest.Extensions, etc.)
- [ ] Add comprehensive unit tests
- [ ] Document custom retry attribute requirements

**Test Cases:**
```csharp
// MSTestRetryDetectorTests.cs
- Should_Detect_Custom_Retry_Attributes
- Should_Extract_MaxRetries_Property
- Should_Extract_Delay_Property
- Should_Get_Attempt_From_TestContext_Properties
- Should_Return_Null_For_Non_Retry_Test
- Should_Handle_Missing_Properties_Gracefully
- Should_Support_Multiple_Retry_Attribute_Patterns
```

---

### 5.2 Update XpingTestBase

**Location:** `src/Xping.Sdk.MSTest/XpingTestBase.cs`

**Changes:**

1. Add field for retry detector:
```csharp
private static readonly MSTestRetryDetector RetryDetector = new();
```

2. Update `CreateTestExecution` method:
```csharp
private static TestExecution CreateTestExecution(
    TestContext context,
    DateTime startTime,
    DateTime endTime,
    TimeSpan duration,
    string threadId,
    string className)
{
    // ... existing code ...

    // Detect retry metadata
    var retryMetadata = RetryDetector.DetectRetryMetadata(context);
    
    var testExecution = new TestExecution
    {
        // ... existing properties ...
        Retry = retryMetadata,  // NEW property
    };

    // ... rest of existing code ...
    
    return testExecution;
}
```

**Tasks:**
- [ ] Add `MSTestRetryDetector` field
- [ ] Update `CreateTestExecution` to detect retry metadata
- [ ] Add integration tests with custom retry attributes
- [ ] Document how to make custom retry attributes detectable
- [ ] Test with popular MSTest retry libraries

---

## Phase 6: Testing & Validation (Week 3, Days 1-3)

### 6.1 Unit Tests

**Coverage Requirements:**
- All retry detectors: >90% code coverage
- RetryMetadata model: 100% coverage
- RetryAttributeRegistry: 100% coverage

**Test Projects:**
- `tests/Xping.Sdk.Core.Tests/Retry/` (new directory)
- `tests/Xping.Sdk.XUnit.Tests/` (update existing)
- `tests/Xping.Sdk.NUnit.Tests/` (update existing)
- `tests/Xping.Sdk.MSTest.Tests/` (update existing)

**Unit Test Files to Create:**
```
Xping.Sdk.Core.Tests/
└── Retry/
    ├── RetryMetadataTests.cs
    └── RetryAttributeRegistryTests.cs

Xping.Sdk.XUnit.Tests/
└── Retry/
    ├── XUnitRetryDetectorTests.cs
    └── XUnitRetryAttributeHelperTests.cs

Xping.Sdk.NUnit.Tests/
└── Retry/
    ├── NUnitRetryDetectorTests.cs
    └── NUnitRetryAttributeHelperTests.cs

Xping.Sdk.MSTest.Tests/
└── Retry/
    ├── MSTestRetryDetectorTests.cs
    └── MSTestRetryAttributeHelperTests.cs
```

**Tasks:**
- [ ] Create all unit test files
- [ ] Implement comprehensive test cases
- [ ] Mock framework-specific types appropriately
- [ ] Test edge cases (null values, missing properties, etc.)
- [ ] Verify thread safety
- [ ] Run tests with coverage analysis

---

### 6.2 Integration Tests

**Location:** `tests/Xping.Sdk.Integration.Tests/Retry/`

**Test Scenarios:**

**XUnit Integration:**
```csharp
// XUnitRetryIntegrationTests.cs
public class XUnitRetryIntegrationTests
{
    [Fact]
    public void Should_Capture_RetryFact_Metadata()
    {
        // Test actual execution with [RetryFact] attribute
        // Verify RetryMetadata is populated correctly
    }

    [Fact]
    public void Should_Track_Multiple_Retry_Attempts()
    {
        // Execute test that fails first, passes on retry
        // Verify attempt numbering
    }

    [Fact]
    public void Should_Handle_Test_Passing_On_First_Attempt()
    {
        // Test with retry configured but passes immediately
        // Verify PassedOnRetry = false
    }
}
```

**NUnit Integration:**
```csharp
// NUnitRetryIntegrationTests.cs
public class NUnitRetryIntegrationTests
{
    [Test, Retry(3)]
    public void Should_Capture_Retry_Attribute_Metadata()
    {
        // Test actual execution with [Retry] attribute
        // Verify RetryMetadata is populated
    }

    [Test]
    public void Should_Track_CurrentRepeatCount()
    {
        // Verify attempt number matches CurrentRepeatCount
    }
}
```

**MSTest Integration:**
```csharp
// MSTestRetryIntegrationTests.cs
[TestClass]
public class MSTestRetryIntegrationTests : XpingTestBase
{
    [TestMethod]
    [CustomRetry(MaxRetries = 3)]
    public void Should_Capture_Custom_Retry_Metadata()
    {
        // Test with custom retry attribute
        // Verify detection works
    }
}
```

**Tasks:**
- [ ] Create integration test files
- [ ] Set up test fixtures with retry scenarios
- [ ] Test with actual retry libraries (xunit.extensions.retry, etc.)
- [ ] Verify data flows through to TestExecution
- [ ] Test serialization to JSON
- [ ] Verify upload to mock Xping API

---

### 6.3 End-to-End Tests

**Sample Applications:**

Update existing sample apps to include retry scenarios:

```
samples/
├── SampleApp.XUnit/
│   └── RetryTests.cs (NEW)
├── SampleApp.NUnit/
│   └── RetryTests.cs (NEW)
└── SampleApp.MSTest/
    └── RetryTests.cs (NEW)
```

**XUnit Sample:**
```csharp
// SampleApp.XUnit/RetryTests.cs
public class RetryTests
{
    private static int _attemptCount = 0;

    [RetryFact(MaxRetries = 3)]
    public void Flaky_Test_Passes_On_Second_Attempt()
    {
        _attemptCount++;
        Assert.True(_attemptCount >= 2, "Should pass on second attempt");
    }

    [RetryFact(MaxRetries = 2, Delay = 100)]
    public void Test_With_Delay_Between_Retries()
    {
        // Test with configured delay
        Assert.True(true);
    }
}
```

**NUnit Sample:**
```csharp
// SampleApp.NUnit/RetryTests.cs
public class RetryTests
{
    private static int _attemptCount = 0;

    [Test, Retry(3)]
    public void Flaky_Test_Passes_On_Second_Attempt()
    {
        _attemptCount++;
        Assert.That(_attemptCount, Is.GreaterThanOrEqualTo(2));
    }

    [Test, Retry(2)]
    public void Test_That_Passes_Immediately()
    {
        Assert.Pass();
    }
}
```

**Tasks:**
- [ ] Add retry test files to all sample projects
- [ ] Include various retry scenarios
- [ ] Document how to run samples
- [ ] Verify data appears in Xping dashboard
- [ ] Create screenshots/examples for documentation

---

## Phase 7: Documentation (Week 3, Days 4-5)

### 7.1 API Documentation

**Files to Create/Update:**
```
docs/
├── features/
│   └── retry-detection.md (NEW)
├── api/
│   ├── retry-metadata.md (NEW)
│   └── retry-detection-api.md (NEW)
└── guides/
    └── working-with-retries.md (NEW)
```

**Content Outline:**

**retry-detection.md:**
- Feature overview
- Why retry detection matters
- Supported frameworks and retry mechanisms
- How it works (architecture)
- Configuration options
- Limitations and known issues

**retry-metadata.md:**
- RetryMetadata class reference
- Property descriptions with examples
- JSON schema
- Usage examples

**working-with-retries.md:**
- How to add retry detection to your tests
- Framework-specific examples
- Best practices for retry strategies
- Interpreting retry data in Xping dashboard
- Common patterns and anti-patterns

**Tasks:**
- [ ] Write comprehensive documentation
- [ ] Add code examples for all frameworks
- [ ] Create diagrams explaining data flow
- [ ] Document custom retry attribute requirements
- [ ] Add troubleshooting section

---

### 7.2 README Updates

Update framework-specific READMEs:

**Files to Update:**
- `src/Xping.Sdk.XUnit/README.md`
- `src/Xping.Sdk.NUnit/README.md`
- `src/Xping.Sdk.MSTest/README.md`
- `README.md` (root)

**Sections to Add:**

```markdown
## Retry Detection

The Xping SDK automatically detects and tracks test retry behavior, helping you identify flaky tests that pass only after retry.

### Supported Retry Mechanisms

#### XUnit
- `[RetryFact]` (xunit.extensions.retry)
- `[RetryTheory]` (xunit.extensions.retry)
- Custom retry attributes

#### NUnit
- `[Retry(n)]` (native NUnit attribute)

#### MSTest
- Custom retry attributes (MSTest.Extensions, custom implementations)

### Captured Metadata

For each test with retry configuration, Xping captures:
- Current attempt number (1 = first attempt, 2+ = retry)
- Maximum retry count
- Whether test passed on retry
- Delay between retries
- Retry reason (if specified)

### Example

```csharp
[RetryFact(MaxRetries = 3)]
public void FlakyTest()
{
    // Test code
}
```

Xping will capture retry metadata including:
- Attempt number for each execution
- Whether it passed on first try or after retry
- Total retry configuration

### Custom Retry Attributes

To make custom retry attributes detectable:

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class CustomRetryAttribute : Attribute
{
    public int MaxRetries { get; set; }
    public int DelayMilliseconds { get; set; }
    public string? Reason { get; set; }
}
```

Register with Xping:
```csharp
RetryAttributeRegistry.RegisterCustomRetryAttribute("XUnit", "CustomRetry");
```
```

**Tasks:**
- [ ] Update all README files
- [ ] Add retry detection sections
- [ ] Include framework-specific examples
- [ ] Update feature lists
- [ ] Add visual examples/screenshots

---

### 7.3 XML Documentation

**Tasks:**
- [ ] Verify all public APIs have XML documentation
- [ ] Add `<example>` tags with code samples
- [ ] Add `<remarks>` explaining retry detection patterns
- [ ] Document thread-safety considerations
- [ ] Generate API reference docs with DocFX

---

## Phase 8: Performance & Optimization (Week 4, Days 1-2)

### 8.1 Performance Testing

**Metrics to Measure:**
- Detection overhead per test (<1ms target)
- Memory allocation impact (minimal)
- Reflection caching effectiveness
- Attribute lookup performance

**Test Scenarios:**
```csharp
[Benchmark]
public void Detect_Retry_Metadata_XUnit()
{
    // Measure detection time for XUnit tests
}

[Benchmark]
public void Detect_Retry_Metadata_NUnit()
{
    // Measure detection time for NUnit tests
}

[Benchmark]
public void Detect_Retry_Metadata_MSTest()
{
    // Measure detection time for MSTest tests
}
```

**Tasks:**
- [ ] Create BenchmarkDotNet project
- [ ] Measure baseline performance
- [ ] Identify bottlenecks
- [ ] Optimize reflection usage
- [ ] Implement caching where appropriate
- [ ] Verify no measurable impact on test execution time

---

### 8.2 Optimization

**Optimization Opportunities:**

1. **Attribute Caching:**
```csharp
private static readonly ConcurrentDictionary<string, RetryMetadata?> _retryCache = new();

public RetryMetadata? DetectRetryMetadata(ITest test)
{
    var testKey = GetTestKey(test);
    return _retryCache.GetOrAdd(testKey, _ => DetectRetryMetadataInternal(test));
}
```

2. **Lazy Detection:**
- Only detect retry metadata when test fails (for initial attempt)
- Detect on all attempts once retry is confirmed

3. **Reflection Optimization:**
- Cache MethodInfo lookups
- Cache attribute type lookups
- Use compiled expressions for property access

**Tasks:**
- [ ] Implement caching strategy
- [ ] Test cache effectiveness
- [ ] Measure performance improvements
- [ ] Ensure thread-safety
- [ ] Document caching behavior

---

## Phase 9: Release Preparation (Week 4, Days 3-5)

### 9.1 Code Review Checklist

**Architecture Review:**
- [ ] Follows existing SDK patterns
- [ ] Consistent with framework adapters
- [ ] Proper separation of concerns
- [ ] Extensibility for future enhancements

**Code Quality:**
- [ ] All methods have XML documentation
- [ ] Follows C# coding conventions
- [ ] No code duplication
- [ ] Proper error handling
- [ ] Thread-safe where required

**Testing:**
- [ ] Unit test coverage >90%
- [ ] Integration tests pass
- [ ] E2E tests pass
- [ ] Performance tests pass
- [ ] Edge cases covered

**Documentation:**
- [ ] API documentation complete
- [ ] User guides written
- [ ] Examples provided
- [ ] README files updated
- [ ] Migration guide (if needed)

---

### 9.2 Package Updates

**NuGet Package Changes:**

Update package versions for all four packages:
- `Xping.Sdk.Core` → 1.1.0
- `Xping.Sdk.XUnit` → 1.1.0
- `Xping.Sdk.NUnit` → 1.1.0
- `Xping.Sdk.MSTest` → 1.1.0

**Release Notes:**
```markdown
## v1.1.0 - Retry Strategy Detection

### New Features
- ✨ Automatic retry detection for all frameworks
- 📊 Retry metadata tracking (attempt number, max retries, delays)
- 🎯 Identify tests that pass only after retry
- 📈 Enhanced flaky test detection with retry patterns

### Framework Support
- **XUnit**: RetryFact, RetryTheory, custom attributes
- **NUnit**: Native [Retry] attribute
- **MSTest**: Custom retry attributes

### API Changes
- Added `RetryMetadata` model
- Added `Retry` property to `TestExecution`
- Added `IRetryDetector<T>` interface
- Added `RetryAttributeRegistry` for extensibility

### Breaking Changes
None - fully backward compatible

### Documentation
- New guides for retry detection
- API reference for retry metadata
- Framework-specific examples
```

**Tasks:**
- [ ] Update version numbers in all .csproj files
- [ ] Update `Directory.Build.props` if needed
- [ ] Write release notes
- [ ] Update CHANGELOG.md
- [ ] Tag release in git
- [ ] Build NuGet packages
- [ ] Test package installation

---

### 9.3 Migration Guide

**For Existing Users:**

Good news! No migration required. Retry detection is automatic and backward compatible.

**What You Get Automatically:**
1. Retry metadata captured for configured retry tests
2. No code changes needed
3. Existing tests continue to work unchanged

**Optional Enhancements:**

If you want to track custom retry attributes:
```csharp
// Register custom retry attribute
RetryAttributeRegistry.RegisterCustomRetryAttribute("XUnit", "MyCustomRetry");
```

**Tasks:**
- [ ] Create migration guide document
- [ ] Test upgrade path from v1.0.x to v1.1.0
- [ ] Document any behavioral changes
- [ ] Provide rollback instructions if needed

---

## Risk Assessment & Mitigation

### Technical Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Reflection overhead | Medium | Low | Cache results, lazy detection |
| Framework API changes | High | Low | Use stable APIs, version compatibility testing |
| Custom retry attributes | Medium | Medium | Clear documentation, registry pattern |
| Thread safety issues | High | Low | Comprehensive concurrency testing |
| Incomplete detection | Medium | Medium | Fallback to safe defaults, logging |

### Timeline Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Framework complexity | +1 week | Start with XUnit (most common), iterate |
| Testing bottleneck | +3 days | Parallel test development with implementation |
| Documentation delay | +2 days | Write docs as you code |

---

## Success Metrics

### Development Metrics
- ✅ All unit tests passing (>90% coverage)
- ✅ All integration tests passing
- ✅ Zero performance regression
- ✅ All documentation complete

### User Metrics (Post-Release)
- Track adoption rate (% of tests with retry attributes)
- Measure flaky test discovery improvement
- Monitor SDK performance impact
- Gather user feedback on retry insights

---

## Dependencies

### Required NuGet Packages

**No new dependencies required!** All frameworks already referenced:
- `xunit.extensibility.core` (already in Xping.Sdk.XUnit)
- `NUnit` (already in Xping.Sdk.NUnit)
- `MSTest.TestFramework` (already in Xping.Sdk.MSTest)

### Optional Testing Dependencies

For integration tests with actual retry libraries:
```xml
<!-- Test projects only -->
<PackageReference Include="xunit.extensions.retry" Version="2.0.0" />
<PackageReference Include="MSTest.Extensions" Version="1.0.0" />
```

---

## Rollout Plan

### Phase 1: Internal Testing (Week 4)
- [ ] Deploy to staging environment
- [ ] Run full test suite
- [ ] Manual testing with all frameworks
- [ ] Performance validation

### Phase 2: Beta Release (Week 5)
- [ ] Release as v1.1.0-beta1
- [ ] Announce to beta testers
- [ ] Collect feedback
- [ ] Fix any issues

### Phase 3: GA Release (Week 6)
- [ ] Release v1.1.0
- [ ] Update documentation site
- [ ] Publish blog post/announcement
- [ ] Monitor for issues

---

## Open Questions

1. **Custom Retry Logic**: Should we support retry logic outside of attributes (e.g., manual retry loops)?
   - **Decision**: Phase 2 feature, focus on attributes first

2. **Retry Reason Inference**: Can we infer retry reason from exception types?
   - **Decision**: Nice-to-have, document as future enhancement

3. **Cross-Framework Retry**: How to handle tests that use multiple retry mechanisms?
   - **Decision**: Capture all detected retry metadata in `AdditionalMetadata`

4. **Parallel Execution**: How does retry interact with parallel test execution?
   - **Decision**: Treat each parallel run independently, leverage existing `ExecutionContext`

---

## Future Enhancements

### Phase 2 Ideas
- **Retry Pattern Analysis**: ML-based detection of "always fails on attempt N" patterns
- **Smart Retry Suggestions**: Recommend retry configuration based on historical data
- **Retry Cost Tracking**: Calculate time wasted on retries
- **Retry Reason Inference**: Auto-categorize flakiness (timing, network, etc.)
- **Custom Retry Strategies**: Support for exponential backoff, jitter, etc.

---

## References

### Framework Documentation
- [xUnit Extensibility](https://xunit.net/docs/getting-started/netfx/visual-studio)
- [NUnit Retry Attribute](https://docs.nunit.org/articles/nunit/writing-tests/attributes/retry.html)
- [MSTest Extensibility](https://github.com/microsoft/testfx/blob/main/docs/README.md)

### Retry Libraries
- [xunit.extensions.retry](https://github.com/jwooley/xunit-retry)
- [MSTest.Extensions](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Extensions)

### Related Issues
- Track implementation in GitHub Issues
- Link to feature request discussions

---

## Appendix A: Example Retry Scenarios

### Scenario 1: Test Passes on First Attempt
```csharp
[RetryFact(MaxRetries = 3)]
public void Stable_Test() => Assert.True(true);

// Captured Metadata:
// AttemptNumber: 1
// MaxRetries: 3
// PassedOnRetry: false
```

### Scenario 2: Test Passes on Second Attempt
```csharp
private static int _count = 0;

[RetryFact(MaxRetries = 3)]
public void Flaky_Test()
{
    _count++;
    Assert.True(_count >= 2);
}

// First Execution:
// AttemptNumber: 1, Outcome: Failed, PassedOnRetry: false

// Second Execution:
// AttemptNumber: 2, Outcome: Passed, PassedOnRetry: true
```

### Scenario 3: Test Fails All Retries
```csharp
[RetryFact(MaxRetries = 2)]
public void Always_Fails() => Assert.False(true);

// Three Executions Captured:
// Attempt 1: Failed
// Attempt 2: Failed
// Attempt 3: Failed (final)
```

---

## Appendix B: JSON Schema

Example of `TestExecution` with retry metadata:

```json
{
  "executionId": "a1b2c3d4-e5f6-7890-1234-567890abcdef",
  "identity": {
    "testId": "stable-hash-id-123",
    "fullyQualifiedName": "MyTests.FlakyTest"
  },
  "testName": "FlakyTest",
  "outcome": "Passed",
  "duration": "00:00:00.1234567",
  "startTimeUtc": "2025-11-12T10:00:00Z",
  "endTimeUtc": "2025-11-12T10:00:00.123Z",
  "retry": {
    "attemptNumber": 2,
    "maxRetries": 3,
    "passedOnRetry": true,
    "delayBetweenRetries": "00:00:00.100",
    "retryReason": "NetworkError",
    "retryAttributeName": "RetryFact",
    "additionalMetadata": {
      "RetryFilter": "TimeoutException"
    }
  }
}
```

---

## Sign-off

**Implementation Plan Approved By:**
- [ ] Technical Lead
- [ ] Product Owner
- [ ] QA Lead

**Start Date:** TBD  
**Target Completion:** TBD  
**Actual Completion:** TBD

---

*This implementation plan is a living document and will be updated as development progresses.*
