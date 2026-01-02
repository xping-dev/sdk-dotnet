# Flaky Test Detection - Critical Requirements

**Project:** Xping SDK & Platform  
**Purpose:** Comprehensive requirements for accurate flaky test detection and confidence scoring  
**Last Updated:** November 3, 2025

---

## Overview

This document outlines the critical missing pieces required to implement robust flaky test detection and accurate confidence scoring. These requirements go beyond basic pass/fail tracking to identify truly flaky tests while distinguishing them from consistently broken tests.

---

## 🔴 Critical Requirements for MVP

### 1. Test Identity & Tracking

**Problem:** How do you uniquely identify the same test across runs, environments, code changes, and refactorings?

**Challenge:**
- Test names can change during refactoring
- Parameterized tests create multiple variations
- Same test runs in different contexts (CI, local, Docker)

**Requirements:**

#### 1.1 Stable Test ID Generation
- Generate deterministic hash from test metadata
- Must be consistent across:
  - Different machines
  - Different environments (CI vs local)
  - Different .NET versions
- Should survive minor formatting changes

**Implementation:**
```csharp
public class TestIdentity
{
    public string TestId { get; set; }           // SHA256(FQN + params)
    public string FullyQualifiedName { get; set; }
    public string Assembly { get; set; }
    public string Namespace { get; set; }
    public string ClassName { get; set; }
    public string MethodName { get; set; }
    public string? ParameterHash { get; set; }   // For parameterized tests
    public string? DisplayName { get; set; }     // Human-readable
}

// Hash algorithm
public static string GenerateTestId(TestIdentity identity)
{
    var canonical = $"{identity.FullyQualifiedName}|{identity.ParameterHash ?? ""}";
    return SHA256Hash(canonical);
}
```

#### 1.2 Parameterized Test Handling
- **xUnit `[Theory]`** - Hash inline data values
- **NUnit `[TestCase]`** - Hash test case parameters
- **MSTest `[DataRow]`** - Hash data row values

```csharp
// Example for [Theory]
[Theory]
[InlineData(2, 3, 5)]
[InlineData(10, -5, 5)]
public void Add_ReturnsSum(int a, int b, int expected)
{
    // TestId for first case: SHA256("MyTests.Calculator.Add_ReturnsSum|2,3,5")
    // TestId for second case: SHA256("MyTests.Calculator.Add_ReturnsSum|10,-5,5")
}
```

#### 1.3 Test Renaming Detection
- Store both `TestId` (stable hash) and `FullyQualifiedName` (current name)
- When FQN changes but code structure similar:
  - **Option A:** Treat as new test (simpler, MVP approach)
  - **Option B:** Use fuzzy matching to migrate history (post-MVP)

**Decision for MVP:** Treat renamed tests as new tests. Provide manual migration tool in platform UI.

Test Renaming Behavior

##### MVP Strategy: Renamed Tests Get New IDs

When you rename a test, it gets a **new TestId**:

```csharp
// BEFORE
[Fact]
public void CalculateSum() { }
// TestId: "abc123..."

// AFTER RENAME
[Fact]
public void Add() { }
// TestId: "def456..." (NEW ID)
```

##### Why?

- **Simplicity**: No complex fuzzy matching in MVP
- **Clarity**: Explicit about what changed
- **Safety**: No risk of incorrect associations

##### Platform Handling

The platform can detect potential renames by:

1. **Similarity Matching**: Compare new test names to recently disappeared tests
2. **Manual Migration**: Provide UI to link old → new test
3. **History Transfer**: Move historical data to new ID

```javascript
// Platform detection example
{
  "disappeared_tests": [
    { "test_id": "abc123", "name": "CalculateSum", "last_seen": "2 days ago" }
  ],
  "new_tests": [
    { "test_id": "def456", "name": "Add", "similarity": 0.75 }
  ],
  "suggestions": [
    {
      "old_id": "abc123",
      "new_id": "def456",
      "reason": "Similar name in same class",
      "confidence": "medium"
    }
  ]
}
```

---

### 2. Minimum Data Threshold for Confidence Scoring

**Problem:** A test that has only run once shows 100% pass rate, but this doesn't mean it's reliable.

**Challenge:**
- New tests have insufficient data
- Low sample size creates high variance
- Need to balance "no data" vs "unreliable score"

**Requirements:**

#### 2.1 Minimum Runs Threshold
```csharp
public class ConfidenceThresholds
{
    public const int MinimumRunsForScore = 10;        // Need 10+ runs
    public const int ConfidentScoreRuns = 50;         // Highly confident at 50+
    public const int RecentRunsWindow = 100;          // Last 100 runs
}

public class ConfidenceCalculator
{
    public ConfidenceResult Calculate(TestHistory history)
    {
        if (history.TotalRuns < ConfidenceThresholds.MinimumRunsForScore)
        {
            return new ConfidenceResult
            {
                Score = null,
                Status = "Insufficient Data",
                Message = $"Need {ConfidenceThresholds.MinimumRunsForScore - history.TotalRuns} more runs"
            };
        }
        
        // Calculate score...
    }
}
```

#### 2.2 Recency Weighting
- Recent runs should weigh more than historical runs
- Sliding window approach: last N runs vs all-time
- Apply exponential decay to older data

```csharp
public class RecencyWeightedScore
{
    public double Calculate(List<TestRun> runs)
    {
        var recentRuns = runs.TakeLast(50).ToList();
        var allRuns = runs;
        
        var recentScore = CalculateRawScore(recentRuns);
        var historicalScore = CalculateRawScore(allRuns);
        
        // 70% weight on recent, 30% on historical
        return 0.7 * recentScore + 0.3 * historicalScore;
    }
    
    private double CalculateRawScore(List<TestRun> runs)
    {
        var passRate = runs.Count(r => r.Passed) / (double)runs.Count;
        var variance = CalculateVariance(runs.Select(r => r.Duration));
        var retryRate = runs.Count(r => r.AttemptNumber > 1) / (double)runs.Count;
        
        return 0.5 * passRate + 0.3 * (1 - variance) + 0.2 * (1 - retryRate);
    }
}
```

#### 2.3 Confidence Level Indicator
Show users how confident the system is about the score:

```csharp
public enum ConfidenceLevel
{
    NoData,          // < 10 runs
    Low,             // 10-24 runs
    Medium,          // 25-49 runs
    High             // 50+ runs
}

// In UI:
// "Confidence Score: 0.85 (High Confidence - based on 127 runs)"
// "Confidence Score: 0.92 (Low Confidence - based on 12 runs)"
```

#### 2.4 Bootstrapping Strategy for New Tests
```csharp
public class NewTestStrategy
{
    public double GetInitialScore(TestExecution firstRun)
    {
        // Start with neutral score
        if (firstRun.Outcome == TestOutcome.Passed)
            return 0.75; // Optimistic but not perfect
        else
            return 0.50; // Could be flaky or broken
    }
}
```

---

### 3. Failure Root Cause Categorization

**Problem:** Not all failures indicate flakiness. Need to distinguish between:
- **Flaky tests** - Intermittent failures (timing, race conditions)
- **Broken tests** - Consistently failing (actual bugs)
- **Environment issues** - Infrastructure problems

**Requirements:**

#### 3.1 Exception Pattern Tracking
```csharp
public class FailureMetadata
{
    public string? ExceptionType { get; set; }          // "TimeoutException"
    public string? ErrorMessage { get; set; }           // Full message
    public string? ErrorMessageHash { get; set; }       // Hash for grouping
    public string? StackTrace { get; set; }
    public string? StackTraceHash { get; set; }         // Hash for grouping
    public bool IsTransient { get; set; }               // Timeout, network, etc.
    public FailureCategory Category { get; set; }
}

public enum FailureCategory
{
    Unknown,
    Timeout,              // TimeoutException, TaskCanceledException
    NetworkFailure,       // HttpRequestException, SocketException
    ResourceContention,   // OutOfMemoryException, ThreadAbortException
    Assertion,            // Assert.* failures
    NullReference,        // NullReferenceException
    ConcurrencyIssue,     // Race conditions, deadlocks
    EnvironmentIssue,     // FileNotFoundException, missing config
    Infrastructure        // CI runner issues, Docker crashes
}
```

#### 3.2 Transient Failure Detection
```csharp
public class TransientFailureDetector
{
    private static readonly HashSet<string> TransientExceptions = new()
    {
        "System.TimeoutException",
        "System.Net.Http.HttpRequestException",
        "System.Net.Sockets.SocketException",
        "System.Threading.Tasks.TaskCanceledException",
        "System.OperationCanceledException",
        "Microsoft.Data.SqlClient.SqlException", // Transient SQL errors
    };
    
    public bool IsTransient(FailureMetadata failure)
    {
        if (TransientExceptions.Contains(failure.ExceptionType))
            return true;
            
        // Check error message patterns
        if (failure.ErrorMessage?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true)
            return true;
            
        if (failure.ErrorMessage?.Contains("connection refused", StringComparison.OrdinalIgnoreCase) == true)
            return true;
            
        return false;
    }
}
```

#### 3.3 Failure Clustering Analysis
```csharp
public class FailureClusterAnalyzer
{
    public FailurePattern Analyze(List<TestRun> failedRuns)
    {
        var errorGroups = failedRuns
            .GroupBy(r => r.Failure.StackTraceHash)
            .OrderByDescending(g => g.Count())
            .ToList();
        
        if (errorGroups.Count == 1)
        {
            // Always fails with same error → Consistently broken
            return new FailurePattern
            {
                Type = FailurePatternType.ConsistentFailure,
                Confidence = 0.0,  // Not flaky, just broken
                Recommendation = "Fix the underlying bug"
            };
        }
        else if (errorGroups.Count > 3)
        {
            // Fails with many different errors → Highly flaky
            return new FailurePattern
            {
                Type = FailurePatternType.HighlyFlaky,
                Confidence = 0.3,
                Recommendation = "Test has multiple failure modes"
            };
        }
        else
        {
            // Mix of errors → Moderately flaky
            return new FailurePattern
            {
                Type = FailurePatternType.IntermittentFailure,
                Confidence = 0.5,
                Recommendation = "Review timing and dependencies"
            };
        }
    }
}

public enum FailurePatternType
{
    ConsistentFailure,      // Same error every time
    IntermittentFailure,    // 2-3 different errors
    HighlyFlaky,            // Many different errors
    NoPattern               // Insufficient data
}
```

#### 3.4 Confidence Score Adjustment
```csharp
public class ConfidenceAdjuster
{
    public double AdjustForFailurePattern(double baseScore, FailurePattern pattern)
    {
        return pattern.Type switch
        {
            FailurePatternType.ConsistentFailure => 0.0,     // Always broken
            FailurePatternType.IntermittentFailure => baseScore * 0.7,
            FailurePatternType.HighlyFlaky => baseScore * 0.4,
            _ => baseScore
        };
    }
}
```

---

### 4. Environment Correlation Matrix

**Problem:** A test might be:
- ✅ Stable on Windows but ❌ flaky on Linux
- ✅ Reliable locally but ❌ flaky in CI
- ✅ Works in Docker but ❌ fails on bare metal

**Requirements:**

#### 4.1 Environment Key Generation
```csharp
public class EnvironmentKey
{
    public string OS { get; set; }              // Windows, Linux, macOS
    public string ExecutionMode { get; set; }   // Local, CI, Docker
    public string? CIProvider { get; set; }     // GitHub, Azure, Jenkins
    public string? RuntimeVersion { get; set; } // .NET 8.0, .NET 6.0
    
    public string ToKey()
    {
        return $"{OS}-{ExecutionMode}-{RuntimeVersion}";
        // Example: "Linux-CI-.NET8.0"
    }
}
```

#### 4.2 Per-Environment Confidence Scores
```csharp
public class TestConfidence
{
    public string TestId { get; set; }
    public double OverallScore { get; set; }
    public List<EnvironmentScore> EnvironmentScores { get; set; }
}

public class EnvironmentScore
{
    public string EnvironmentKey { get; set; }    // "Windows-Local-.NET8.0"
    public double Score { get; set; }             // 0.0 - 1.0
    public int SampleSize { get; set; }           // Number of runs
    public ConfidenceLevel ConfidenceLevel { get; set; }
    
    // Breakdown
    public double PassRate { get; set; }
    public double AverageDuration { get; set; }
    public double DurationVariance { get; set; }
    public int RetryCount { get; set; }
}
```

#### 4.3 Environment-Specific Insights
```csharp
public class EnvironmentInsights
{
    public string TestId { get; set; }
    public List<EnvironmentIssue> Issues { get; set; }
}

public class EnvironmentIssue
{
    public string EnvironmentKey { get; set; }
    public string Severity { get; set; }          // Critical, Warning, Info
    public string Message { get; set; }
    
    // Examples:
    // "Flaky on Linux-CI: Pass rate 65% (vs 95% on Windows-Local)"
    // "Always fails on macOS-Docker: 0% pass rate"
    // "Slow on GitHub-Actions: 3x slower than local runs"
}
```

#### 4.4 Cross-Environment Comparison
```csharp
public class EnvironmentComparison
{
    public string BestEnvironment { get; set; }
    public double BestScore { get; set; }
    
    public string WorstEnvironment { get; set; }
    public double WorstScore { get; set; }
    
    public double EnvironmentVariance { get; set; }
    
    public bool IsEnvironmentSensitive()
    {
        return Math.Abs(BestScore - WorstScore) > 0.3;
    }
}

// In UI:
// "✅ Stable on Windows-Local (0.95) but ❌ Flaky on Linux-CI (0.62)"
// "⚠️ Environment-sensitive test - varies by 33%"
```

---

### 5. Test Dependency & Order Detection

**Problem:** Some tests only fail when:
- Run after specific other tests
- Run in parallel with certain tests
- Run as part of a larger test suite
- Run in a specific order

**Requirements:**

#### 5.1 Execution Context Tracking
```csharp
public class ExecutionContext
{
    // Test Ordering
    public int PositionInSuite { get; set; }          // 1st, 50th, 200th test
    public string? PreviousTestId { get; set; }       // Test that ran before
    public string? PreviousTestName { get; set; }
    public TestOutcome? PreviousTestOutcome { get; set; }
    
    // Parallelization
    public bool WasParallelized { get; set; }
    public int ConcurrentTestCount { get; set; }      // How many tests in parallel
    public int ThreadId { get; set; }
    
    // Test Suite Context
    public string TestSuiteId { get; set; }           // Unique per test run
    public int TotalTestsInSuite { get; set; }
    public TimeSpan SuiteElapsedTime { get; set; }   // Time since suite started
}
```

#### 5.2 Dependency Correlation Analysis
```csharp
public class DependencyAnalyzer
{
    public DependencyInsights Analyze(string testId, List<TestRun> runs)
    {
        var runsWithPrevious = runs.Where(r => r.ExecutionContext.PreviousTestId != null);
        
        var dependencies = runsWithPrevious
            .GroupBy(r => r.ExecutionContext.PreviousTestId)
            .Select(g => new
            {
                PreviousTest = g.Key,
                TotalRuns = g.Count(),
                FailureRate = g.Count(r => r.Failed) / (double)g.Count()
            })
            .OrderByDescending(x => x.FailureRate)
            .ToList();
        
        // Detect statistically significant correlation
        var suspiciousDependencies = dependencies
            .Where(d => d.TotalRuns >= 5 && d.FailureRate > 0.6)
            .ToList();
        
        if (suspiciousDependencies.Any())
        {
            return new DependencyInsights
            {
                HasDependencies = true,
                SuspiciousPredecessors = suspiciousDependencies
                    .Select(d => $"{d.PreviousTest} (fails {d.FailureRate:P0} of the time)")
                    .ToList(),
                Recommendation = "Test may have shared state issues with predecessor tests"
            };
        }
        
        return new DependencyInsights { HasDependencies = false };
    }
}
```

#### 5.3 Parallel Execution Correlation
```csharp
public class ParallelExecutionAnalyzer
{
    public ParallelInsights Analyze(List<TestRun> runs)
    {
        var parallelRuns = runs.Where(r => r.ExecutionContext.WasParallelized).ToList();
        var sequentialRuns = runs.Where(r => !r.ExecutionContext.WasParallelized).ToList();
        
        if (parallelRuns.Count < 5 || sequentialRuns.Count < 5)
            return new ParallelInsights { InsufficientData = true };
        
        var parallelFailureRate = parallelRuns.Count(r => r.Failed) / (double)parallelRuns.Count;
        var sequentialFailureRate = sequentialRuns.Count(r => r.Failed) / (double)sequentialRuns.Count;
        
        var difference = Math.Abs(parallelFailureRate - sequentialFailureRate);
        
        if (difference > 0.3)
        {
            return new ParallelInsights
            {
                IsParallelizationSensitive = true,
                ParallelFailureRate = parallelFailureRate,
                SequentialFailureRate = sequentialFailureRate,
                Recommendation = parallelFailureRate > sequentialFailureRate
                    ? "Test is flaky when run in parallel - likely shared state issue"
                    : "Test is more reliable when run in parallel - investigate sequential dependencies"
            };
        }
        
        return new ParallelInsights { IsParallelizationSensitive = false };
    }
}
```

#### 5.4 Position-in-Suite Correlation
```csharp
public class PositionAnalyzer
{
    public PositionInsights Analyze(List<TestRun> runs)
    {
        var positionGroups = runs
            .GroupBy(r => r.ExecutionContext.PositionInSuite / 50) // Buckets of 50
            .Select(g => new
            {
                PositionRange = $"{g.Key * 50}-{(g.Key + 1) * 50}",
                FailureRate = g.Count(r => r.Failed) / (double)g.Count()
            })
            .ToList();
        
        var variance = CalculateVariance(positionGroups.Select(p => p.FailureRate));
        
        if (variance > 0.2)
        {
            return new PositionInsights
            {
                IsPositionSensitive = true,
                Recommendation = "Test reliability varies by position in suite - may have state accumulation issues"
            };
        }
        
        return new PositionInsights { IsPositionSensitive = false };
    }
}
```

---

### 6. Retry Strategy Detection

**Problem:** Test frameworks and custom code may retry failed tests. Need to understand:
- Did test pass on first attempt or after retry?
- Does it always pass on Nth retry? (indicates timing issue)
- Are retries masking flakiness?

**Requirements:**

#### 6.1 Retry Metadata Tracking
```csharp
public class RetryMetadata
{
    public int AttemptNumber { get; set; }          // 1 = first run, 2+ = retry
    public int MaxRetries { get; set; }             // Configured max retries
    public bool PassedOnRetry { get; set; }         // False if passed on first try
    public TimeSpan DelayBetweenRetries { get; set; }
    public string? RetryReason { get; set; }        // "Timeout", "NetworkError"
}

public class TestExecution
{
    // ... other properties
    
    public RetryMetadata? Retry { get; set; }       // Null if no retry attempted
}
```

#### 6.2 Framework-Specific Retry Detection

**xUnit:**
```csharp
// Detect xUnit retry attributes
[RetryFact(MaxRetries = 3)]
public void FlakyTest() { }

// In SDK:
public class XUnitRetryDetector
{
    public RetryMetadata? DetectRetryAttribute(ITest test)
    {
        var retryAttr = test.TestCase.TestMethod.Method
            .GetCustomAttributes(typeof(RetryFactAttribute))
            .FirstOrDefault();
        
        if (retryAttr != null)
        {
            return new RetryMetadata
            {
                MaxRetries = retryAttr.MaxRetries,
                AttemptNumber = GetCurrentAttempt(test)
            };
        }
        
        return null;
    }
}
```

**NUnit:**
```csharp
// Detect NUnit retry attribute
[Test, Retry(3)]
public void FlakyTest() { }

// In SDK:
public class NUnitRetryDetector
{
    public RetryMetadata? DetectRetryAttribute(ITest test)
    {
        var retryAttr = test.Method.GetCustomAttributes<RetryAttribute>(true)
            .FirstOrDefault();
        
        if (retryAttr != null)
        {
            return new RetryMetadata
            {
                MaxRetries = retryAttr.TryCount,
                AttemptNumber = TestContext.CurrentContext.CurrentRepeatCount + 1
            };
        }
        
        return null;
    }
}
```

#### 6.3 Retry Pattern Analysis
```csharp
public class RetryPatternAnalyzer
{
    public RetryInsights Analyze(List<TestRun> runs)
    {
        var retriedRuns = runs.Where(r => r.Retry?.AttemptNumber > 1).ToList();
        
        if (retriedRuns.Count < 5)
            return new RetryInsights { InsufficientData = true };
        
        // Check if test always passes on Nth attempt
        var successfulRetries = retriedRuns.Where(r => r.Passed).ToList();
        var attemptDistribution = successfulRetries
            .GroupBy(r => r.Retry.AttemptNumber)
            .ToDictionary(g => g.Key, g => g.Count());
        
        if (attemptDistribution.TryGetValue(2, out var secondAttemptSuccesses))
        {
            if (secondAttemptSuccesses / (double)successfulRetries.Count > 0.8)
            {
                return new RetryInsights
                {
                    AlwaysPassesOnRetry = true,
                    TypicalSuccessAttempt = 2,
                    Recommendation = "Test consistently passes on 2nd attempt - likely timing/race condition. Consider increasing timeout or fixing synchronization."
                };
            }
        }
        
        return new RetryInsights
        {
            RequiresRetries = true,
            AverageAttemptsUntilSuccess = successfulRetries.Average(r => r.Retry.AttemptNumber),
            Recommendation = "Test frequently requires retries - investigate root cause"
        };
    }
}
```

#### 6.4 Confidence Score Adjustment for Retries
```csharp
public class RetryConfidenceAdjuster
{
    public double AdjustForRetries(double baseScore, RetryInsights insights)
    {
        if (insights.AlwaysPassesOnRetry)
        {
            // Penalize heavily - test is inherently flaky
            return baseScore * 0.5;
        }
        
        if (insights.RequiresRetries)
        {
            // Moderate penalty
            var retryPenalty = 1.0 - (insights.AverageAttemptsUntilSuccess - 1) * 0.1;
            return baseScore * Math.Max(retryPenalty, 0.6);
        }
        
        return baseScore;
    }
}
```

---

## 📊 Updated Data Model

### Complete TestExecution Model
```csharp
public class TestExecution
{
    // Identity
    public Guid ExecutionId { get; init; }
    public string TestId { get; init; }                    // Stable hash
    public string TestName { get; init; }
    public string FullyQualifiedName { get; init; }
    public string Assembly { get; init; }
    public string Namespace { get; init; }
    public string? ParameterHash { get; init; }            // For parameterized tests
    
    // Outcome
    public TestOutcome Outcome { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTime StartTimeUtc { get; init; }
    public DateTime EndTimeUtc { get; init; }
    
    // Failure Details
    public FailureMetadata? Failure { get; init; }
    
    // Environment
    public EnvironmentInfo Environment { get; init; }
    
    // Execution Context
    public ExecutionContext ExecutionContext { get; init; }
    
    // Retry Information
    public RetryMetadata? Retry { get; init; }
    
    // Resource Metrics
    public ResourceMetrics Resources { get; init; }
    
    // Network Metrics
    public NetworkMetrics? Network { get; init; }
}

public class FailureMetadata
{
    public string? ExceptionType { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorMessageHash { get; set; }
    public string? StackTrace { get; set; }
    public string? StackTraceHash { get; set; }
    public bool IsTransient { get; set; }
    public FailureCategory Category { get; set; }
}

public class ExecutionContext
{
    // Test Ordering
    public int PositionInSuite { get; set; }
    public string? PreviousTestId { get; set; }
    public string? PreviousTestName { get; set; }
    public TestOutcome? PreviousTestOutcome { get; set; }
    
    // Parallelization
    public bool WasParallelized { get; set; }
    public int ConcurrentTestCount { get; set; }
    public int ThreadId { get; set; }
    
    // Test Suite Context
    public string TestSuiteId { get; set; }
    public int TotalTestsInSuite { get; set; }
    public TimeSpan SuiteElapsedTime { get; set; }
}

public class RetryMetadata
{
    public int AttemptNumber { get; set; }
    public int MaxRetries { get; set; }
    public bool PassedOnRetry { get; set; }
    public TimeSpan DelayBetweenRetries { get; set; }
    public string? RetryReason { get; set; }
}

public class ResourceMetrics
{
    public double CpuUsagePercent { get; set; }
    public long MemoryUsedMB { get; set; }
    public long AvailableMemoryMB { get; set; }
    public int ThreadCount { get; set; }
}

public class NetworkMetrics
{
    public int? ApiLatencyMs { get; set; }
    public bool? IsOnline { get; set; }
    public string? ConnectionType { get; set; }
}
```

---

## 🎯 Implementation Priority

### MVP - Must Have
1. ✅ **Test Identity & Stable Hashing** - Critical for tracking tests across runs
2. ✅ **Minimum Runs Threshold** - Prevent misleading scores
3. ✅ **Failure Categorization** - Distinguish flaky from broken
4. ✅ **Per-Environment Scoring** - OS/CI differences are major flakiness indicators
5. ✅ **Retry Detection** - Retries mask flakiness

### MVP - Should Have
6. 🟡 **Test Order Tracking** - Detect dependencies (implement basic version)
7. 🟡 **Recency Weighting** - Recent data more valuable (simple exponential decay)
8. 🟡 **Parallel Execution Detection** - Race conditions common

### Post-MVP - Nice to Have
9. 🟢 **Advanced Dependency Analysis** - Complex correlation patterns
10. 🟢 **Position-in-Suite Analysis** - State accumulation detection
11. 🟢 **Retry Pattern ML** - Predict optimal retry strategy
12. 🟢 **Time-Based Correlation** - Hour/day patterns

---

## 📈 Success Metrics

### Data Quality
- [ ] 95%+ tests have stable IDs across runs
- [ ] <5% false positive flaky test flags
- [ ] <10% false negative (missed flaky tests)

### Confidence Scoring Accuracy
- [ ] Tests with score >0.8 have <10% failure rate in next 100 runs
- [ ] Tests with score <0.5 have >40% failure rate in next 100 runs
- [ ] Environment-specific scores accurate within ±15%

### Developer Experience
- [ ] Developers trust confidence scores (user survey >80% agreement)
- [ ] Flaky test detection leads to actual fixes (track issue creation rate)
- [ ] SDK overhead <5ms per test

---

## 🚀 Next Steps

1. **Update SDK Data Models** - Implement complete `TestExecution` schema
2. **Implement Test ID Generation** - Stable hashing algorithm
3. **Build Confidence Calculator** - With all 6 components
4. **Create Analysis Pipeline** - Process test history and generate insights
5. **Design Platform UI** - Display per-environment scores and recommendations
6. **Write Migration Plan** - Transition existing data to new schema

---

## 📚 References

- [xping-confidence-scoring.md](./xping-confidence-scoring.md) - Confidence scoring algorithm
- [SDK-Implementation-Plan.md](./SDK-Implementation-Plan.md) - Technical implementation details
- [xping-one-pager.md](./xping-one-pager.md) - MVP vision and requirements

---

**Document Version:** 1.0  
**Last Updated:** November 3, 2025  
**Next Review:** After Phase 2 completion
