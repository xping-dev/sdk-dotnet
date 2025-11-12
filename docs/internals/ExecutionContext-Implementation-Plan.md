# Execution Context & Order Detection - Implementation Plan

## Executive Summary

This document outlines the feasibility and implementation plan for adding execution context tracking to Xping SDK, enabling detection of test dependencies, order-related failures, and parallelization issues.

**Status**: ✅ **FEASIBLE** with varying capabilities across frameworks

---

## Problem Statement

Some tests only fail when:
- Run after specific other tests (order dependency)
- Run in parallel with certain tests (concurrency issues)
- Run as part of a larger test suite (resource contention)
- Run in a specific order (initialization/cleanup dependencies)

Currently, Xping SDK cannot detect these patterns because we don't track:
- Test execution order within a suite
- Which test ran before the current test
- Parallelization state
- Thread/worker assignment
- Suite-level timing context

---

## Framework Capabilities Analysis

### 1. XUnit

**Architecture**: Message sink-based interception (`IMessageSink`)

**Capabilities**:
| Feature | Availability | Method |
|---------|-------------|--------|
| Test order | ✅ **YES** | Track message sequence in `XpingMessageSink` |
| Previous test | ✅ **YES** | Maintain ordered list of completed tests |
| Parallelization | ✅ **YES** | XUnit messages include collection info |
| Thread ID | ✅ **YES** | `Environment.CurrentManagedThreadId` or `Thread.CurrentThread.ManagedThreadId` |
| Total tests | ⚠️ **PARTIAL** | Can count discovered tests, but dynamic discovery limits accuracy |
| Suite elapsed time | ✅ **YES** | Track from assembly start to current test |

**Implementation Notes**:
- `XpingMessageSink` intercepts all test messages in sequence
- `ConcurrentDictionary<string, TestExecutionData>` already tracks active tests
- Can extend to maintain execution order and thread mapping
- X Unit runs tests in collections - collections can run in parallel
- Individual tests within a collection run sequentially by default
- Can detect parallel execution by tracking concurrent `ITestStarting` messages

**Limitations**:
- Test discovery happens at runtime, total count may not be available upfront
- Parallel collections make "position in suite" ambiguous (need per-collection position)

---

### 2. NUnit

**Architecture**: Attribute-based interception (`ITestAction`)

**Capabilities**:
| Feature | Availability | Method |
|---------|-------------|--------|
| Test order | ⚠️ **PARTIAL** | Must maintain global counter in `XpingContext` |
| Previous test | ⚠️ **PARTIAL** | Global state in `XpingContext`, but thread-safety concerns |
| Parallelization | ✅ **YES** | `TestContext.CurrentContext.WorkerId` available |
| Thread ID | ✅ **YES** | `Environment.CurrentManagedThreadId` |
| Total tests | ❌ **NO** | Not exposed through `ITestAction` or `TestContext` |
| Suite elapsed time | ✅ **YES** | Track from `XpingSetupFixture.OneTimeSetUp` |

**Implementation Notes**:
- `XpingTrackAttribute.AfterTest()` is called per-test sequentially per thread
- NUnit provides `TestContext.CurrentContext.WorkerId` for parallel worker identification
- Need thread-safe global counter for position tracking
- `TestContext.CurrentContext.Test` provides test hierarchy info
- Can use `ConcurrentDictionary<int, string>` to track last test per worker

**Limitations**:
- No native total test count API
- Parallel execution with `[Parallelizable]` makes global order ambiguous
- Must track per-worker order instead
- Previous test only reliable within same worker thread

---

### 3. MSTest

**Architecture**: Base class with lifecycle hooks (`TestInitialize`/`TestCleanup`)

**Capabilities**:
| Feature | Availability | Method |
|---------|-------------|--------|
| Test order | ⚠️ **PARTIAL** | Must maintain global counter in `XpingContext` |
| Previous test | ⚠️ **PARTIAL** | Global state in `XpingContext`, thread-safety concerns |
| Parallelization | ⚠️ **UNCLEAR** | MSTest V2 supports parallel, but limited API exposure |
| Thread ID | ✅ **YES** | `Environment.CurrentManagedThreadId` |
| Total tests | ❌ **NO** | Not exposed through `TestContext` |
| Suite elapsed time | ✅ **YES** | Track from `AssemblyInitialize` |

**Implementation Notes**:
- `XpingTestBase.XpingTestCleanup()` is called after each test
- `TestContext` is per-test instance provided by framework
- No native API for parallelization state
- Need thread-safe global state in `XpingContext`
- MSTest V2 supports `[TestCategory("parallelizable")]` but API is limited

**Limitations**:
- Weakest API for parallel execution detection
- No worker/thread pool identification
- Global order tracking requires careful thread synchronization
- Previous test tracking only reliable in sequential execution

---

## Proposed ExecutionContext Model

```csharp
/// <summary>
/// Execution context tracking test order, parallelization, and suite state.
/// </summary>
public class ExecutionContext
{
    // Test Ordering
    /// <summary>
    /// Position of this test in the execution order (1-based).
    /// In parallel execution, this is the position within the thread/worker.
    /// </summary>
    public int PositionInSuite { get; set; }
    
    /// <summary>
    /// Global position across all threads (best effort in parallel scenarios).
    /// </summary>
    public int? GlobalPosition { get; set; }
    
    /// <summary>
    /// Stable Test ID of the test that executed immediately before this one.
    /// Null for the first test. In parallel execution, this is per-worker.
    /// </summary>
    public string? PreviousTestId { get; set; }
    
    /// <summary>
    /// Display name of the previous test for debugging.
    /// </summary>
    public string? PreviousTestName { get; set; }
    
    /// <summary>
    /// Outcome of the previous test (Passed, Failed, Skipped, etc.).
    /// </summary>
    public TestOutcome? PreviousTestOutcome { get; set; }
    
    // Parallelization
    /// <summary>
    /// Whether this test was executed in parallel with other tests.
    /// </summary>
    public bool WasParallelized { get; set; }
    
    /// <summary>
    /// Number of tests executing concurrently at the time this test started.
    /// 1 for sequential execution.
    /// </summary>
    public int ConcurrentTestCount { get; set; }
    
    /// <summary>
    /// Thread ID or Worker ID executing this test.
    /// Useful for correlating tests that share the same execution thread.
    /// </summary>
    public string ThreadId { get; set; } = string.Empty;
    
    /// <summary>
    /// Framework-specific worker identifier (e.g., NUnit WorkerId, XUnit Collection).
    /// </summary>
    public string? WorkerId { get; set; }
    
    // Test Suite Context
    /// <summary>
    /// Unique identifier for this test run/session.
    /// All tests in the same run share this ID.
    /// </summary>
    public string TestSuiteId { get; set; } = string.Empty;
    
    /// <summary>
    /// Total number of tests in this suite (if known).
    /// May be null if framework doesn't expose this information.
    /// </summary>
    public int? TotalTestsInSuite { get; set; }
    
    /// <summary>
    /// Time elapsed since the test suite started executing.
    /// Useful for detecting "late in suite" failures.
    /// </summary>
    public TimeSpan SuiteElapsedTime { get; set; }
    
    /// <summary>
    /// Framework-specific collection or fixture name.
    /// XUnit: Collection name, NUnit: Fixture name, MSTest: Test class name.
    /// </summary>
    public string? CollectionName { get; set; }
}
```

---

## Implementation Strategy

### Phase 1: Core Infrastructure

**1.1 Add ExecutionContext to TestExecution**

```csharp
// In TestExecution.cs
public class TestExecution
{
    // ... existing properties ...
    
    /// <summary>
    /// Execution context tracking order, parallelization, and suite state.
    /// </summary>
    public ExecutionContext? Context { get; set; }
}
```

**1.2 Create Execution Tracking Service**

```csharp
// New file: src/Xping.Sdk.Core/Collection/ExecutionTracker.cs
public sealed class ExecutionTracker
{
    private readonly ConcurrentDictionary<string, TestExecutionInfo> _previousTests;
    private readonly ConcurrentDictionary<string, int> _workerPositions;
    private int _globalPosition;
    private DateTime _suiteStartTime;
    private readonly string _suiteId;
    
    public ExecutionTracker()
    {
        _previousTests = new ConcurrentDictionary<string, TestExecutionInfo>();
        _workerPositions = new ConcurrentDictionary<string, int>();
        _suiteStartTime = DateTime.UtcNow;
        _suiteId = Guid.NewGuid().ToString();
    }
    
    public ExecutionContext CreateContext(string workerId, string? collectionName = null)
    {
        var threadId = Environment.CurrentManagedThreadId.ToString();
        var workerKey = workerId ?? threadId;
        
        // Get position for this worker
        var workerPosition = _workerPositions.AddOrUpdate(
            workerKey,
            _ => 1,
            (_, current) => current + 1);
        
        // Get global position (approximate in parallel scenarios)
        var globalPos = Interlocked.Increment(ref _globalPosition);
        
        // Get previous test for this worker
        _previousTests.TryGetValue(workerKey, out var previousTest);
        
        // Count active tests (approximation)
        var concurrentCount = _workerPositions.Count;
        
        return new ExecutionContext
        {
            PositionInSuite = workerPosition,
            GlobalPosition = globalPos,
            PreviousTestId = previousTest?.TestId,
            PreviousTestName = previousTest?.TestName,
            PreviousTestOutcome = previousTest?.Outcome,
            WasParallelized = concurrentCount > 1,
            ConcurrentTestCount = concurrentCount,
            ThreadId = threadId,
            WorkerId = workerId,
            TestSuiteId = _suiteId,
            SuiteElapsedTime = DateTime.UtcNow - _suiteStartTime,
            CollectionName = collectionName
        };
    }
    
    public void RecordTestCompletion(string workerId, string testId, string testName, TestOutcome outcome)
    {
        var threadId = Environment.CurrentManagedThreadId.ToString();
        var workerKey = workerId ?? threadId;
        
        _previousTests[workerKey] = new TestExecutionInfo
        {
            TestId = testId,
            TestName = testName,
            Outcome = outcome
        };
    }
    
    private sealed class TestExecutionInfo
    {
        public string TestId { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public TestOutcome Outcome { get; set; }
    }
}
```

---

### Phase 2: Framework-Specific Implementations

#### 2.1 XUnit Implementation

```csharp
// In XpingMessageSink.cs
public sealed class XpingMessageSink : IMessageSink
{
    private readonly ExecutionTracker _tracker;
    private readonly ConcurrentDictionary<string, int> _activeCollections;
    
    public XpingMessageSink(IMessageSink innerSink)
    {
        // ... existing initialization ...
        _tracker = new ExecutionTracker();
        _activeCollections = new ConcurrentDictionary<string, int>();
    }
    
    private void HandleTestStarting(ITestStarting testStarting)
    {
        var testKey = GetTestKey(testStarting.Test);
        var collectionName = testStarting.Test.TestCase.TestMethod.TestClass.TestCollection.DisplayName;
        
        // Track active collections for parallelization detection
        _activeCollections.AddOrUpdate(collectionName, 1, (_, count) => count + 1);
        
        var data = new TestExecutionData
        {
            Test = testStarting.Test,
            StartTime = DateTime.UtcNow,
            StartTimestamp = Stopwatch.GetTimestamp(),
            CollectionName = collectionName
        };
        
        _testData.TryAdd(testKey, data);
    }
    
    private static TestExecution CreateTestExecution(
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
        string? collectionName,
        ExecutionTracker tracker)
    {
        // ... existing code ...
        
        // Create execution context
        var workerId = collectionName; // Use collection as worker ID
        var context = tracker.CreateContext(workerId, collectionName);
        
        var execution = new TestExecution
        {
            // ... existing properties ...
            Context = context
        };
        
        // Record completion for tracking next test
        tracker.RecordTestCompletion(workerId, identity.TestId, test.DisplayName, outcome);
        
        return execution;
    }
}
```

#### 2.2 NUnit Implementation

```csharp
// In XpingContext.cs
public static class XpingContext
{
    private static ExecutionTracker? _executionTracker;
    
    private static TestExecutionCollector InitializeInternal(XpingConfiguration configuration)
    {
        // ... existing initialization ...
        _executionTracker = new ExecutionTracker();
        return _collector;
    }
    
    internal static ExecutionTracker? ExecutionTracker => _executionTracker;
}

// In XpingTrackAttribute.cs
private static TestExecution CreateTestExecution(
    ITest test,
    DateTime startTime,
    DateTime endTime,
    TimeSpan duration)
{
    var result = TestContext.CurrentContext.Result;
    var workerId = TestContext.CurrentContext.WorkerId; // NUnit-specific
    
    // ... existing code ...
    
    // Create execution context
    var tracker = XpingContext.ExecutionTracker;
    var context = tracker?.CreateContext(workerId, test.ClassName);
    
    var execution = new TestExecution
    {
        // ... existing properties ...
        Context = context
    };
    
    // Record completion
    tracker?.RecordTestCompletion(workerId, identity.TestId, test.Name, MapOutcome(result.Outcome));
    
    return execution;
}
```

#### 2.3 MSTest Implementation

```csharp
// In XpingContext.cs
public static class XpingContext
{
    private static ExecutionTracker? _executionTracker;
    
    private static TestExecutionCollector InitializeInternal(XpingConfiguration configuration)
    {
        // ... existing initialization ...
        _executionTracker = new ExecutionTracker();
        return _collector;
    }
    
    internal static ExecutionTracker? ExecutionTracker => _executionTracker;
}

// In XpingTestBase.cs
private static TestExecution CreateTestExecution(
    TestContext context,
    DateTime startTime,
    DateTime endTime,
    TimeSpan duration)
{
    // ... existing code ...
    
    // MSTest doesn't expose worker ID, use thread ID
    var threadId = Environment.CurrentManagedThreadId.ToString();
    var className = context.FullyQualifiedTestClassName;
    
    // Create execution context
    var tracker = XpingContext.ExecutionTracker;
    var executionContext = tracker?.CreateContext(threadId, className);
    
    var execution = new TestExecution
    {
        // ... existing properties ...
        Context = executionContext
    };
    
    // Record completion
    tracker?.RecordTestCompletion(threadId, identity.TestId, context.TestName, outcome);
    
    return execution;
}
```

---

### Phase 3: Testing & Validation

**3.1 Unit Tests**

- Test `ExecutionTracker` for sequential execution
- Test `ExecutionTracker` for parallel execution
- Test thread-safety with concurrent access
- Test previous test tracking per worker
- Test suite elapsed time calculation

**3.2 Integration Tests**

- Create sample tests with intentional order dependencies
- Create parallel test suites
- Create mixed sequential/parallel scenarios
- Verify context data accuracy across frameworks

**3.3 Sample Applications**

- Add execution context logging to sample apps
- Create "flaky by order" test examples
- Create "flaky by parallelization" test examples

---

## Framework Comparison Matrix

| Feature | XUnit | NUnit | MSTest | Implementation Difficulty |
|---------|-------|-------|--------|---------------------------|
| Position in Suite | ✅ Per collection | ✅ Per worker | ✅ Per thread | Easy |
| Global Position | ⚠️ Approximate | ⚠️ Approximate | ⚠️ Approximate | Easy |
| Previous Test | ✅ Reliable | ✅ Per worker | ⚠️ Per thread | Medium |
| Parallelization Detection | ✅ Collection-based | ✅ Worker-based | ⚠️ Thread-based | Medium |
| Thread ID | ✅ Yes | ✅ Yes | ✅ Yes | Easy |
| Worker ID | ✅ Collection name | ✅ WorkerId | ❌ No API | Easy |
| Total Tests | ❌ Not reliable | ❌ Not exposed | ❌ Not exposed | N/A |
| Suite Elapsed Time | ✅ Yes | ✅ Yes | ✅ Yes | Easy |
| Collection/Fixture Name | ✅ Yes | ✅ Yes | ✅ Class name | Easy |

---

## Recommendations

### Must Have (MVP)
1. ✅ Position in suite (per worker/thread)
2. ✅ Previous test tracking (per worker/thread)
3. ✅ Thread ID
4. ✅ Suite elapsed time
5. ✅ Concurrent test count (approximate)

### Should Have
1. ✅ Worker ID (framework-specific)
2. ✅ Collection/Fixture name
3. ✅ Parallelization detection flag
4. ⚠️ Global position (best effort)

### Nice to Have
1. ❌ Total tests in suite (not reliably available)
2. ❌ Exact concurrent test list (too expensive to track)

---

## Implementation Phases

### Phase 1: Foundation (Week 1)
- [x] Create `ExecutionContext` model
- [x] Create `ExecutionTracker` service
- [x] Add `Context` property to `TestExecution`
- [x] Write unit tests for `ExecutionTracker`

### Phase 2: XUnit Integration (Week 1)
- [x] Integrate `ExecutionTracker` into `XpingMessageSink`
- [x] Track collection-based parallelization
- [x] Add integration tests
- [x] Update XUnit sample app

### Phase 3: NUnit Integration (Week 2)
- [x] Integrate `ExecutionTracker` into `XpingContext`
- [x] Use `WorkerId` for worker tracking
- [x] Add integration tests
- [x] Update NUnit sample app

### Phase 4: MSTest Integration (Week 2)
- [x] Integrate `ExecutionTracker` into `XpingContext`
- [x] Use thread ID for worker tracking
- [x] Add integration tests
- [x] Update MSTest sample app

### Phase 5: Documentation & Polish (Week 3)
- [ ] Update SDK documentation
- [ ] Add usage examples
- [ ] Update API documentation
- [ ] Performance testing and optimization

---

## Risks & Mitigations

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Parallel execution makes order ambiguous | High | High | Track per-worker order instead of global |
| Performance overhead from tracking | Medium | Medium | Use efficient concurrent collections, lazy initialization |
| Thread-safety issues | High | Medium | Use `ConcurrentDictionary`, atomic operations |
| Framework API limitations (MSTest) | Medium | High | Document limitations, use best-effort approach |
| Memory leaks from tracking state | Medium | Low | Clear old data per test session, bounded collections |

---

## Success Criteria

1. ✅ All three frameworks report execution context
2. ✅ Previous test tracking works in sequential scenarios
3. ✅ Previous test tracking works per-worker in parallel scenarios
4. ✅ Parallelization detection is accurate
5. ✅ Thread/Worker ID is consistently reported
6. ✅ Suite elapsed time is accurate
7. ✅ No performance regression (< 5% overhead)
8. ✅ Thread-safe under concurrent execution
9. ✅ Unit test coverage > 90%
10. ✅ Integration tests pass for all frameworks

---

## Open Questions

1. **Q**: Should we track execution order globally or per-worker?  
   **A**: Both. `PositionInSuite` is per-worker, `GlobalPosition` is best-effort global.

2. **Q**: How to handle test discovery happening during execution?  
   **A**: Accept that `TotalTestsInSuite` may be null or inaccurate for dynamic discovery.

3. **Q**: Should we expose raw thread IDs or friendly names?  
   **A**: Both. `ThreadId` for correlation, `WorkerId` for friendly identification.

4. **Q**: How to clean up tracking state after suite completes?  
   **A**: Clear state in assembly cleanup hooks, same as current session cleanup.

5. **Q**: Should context be mandatory or optional?  
   **A**: Optional with null-safe handling to avoid breaking existing code.

---

## Conclusion

Execution context tracking is **feasible and valuable** for Xping SDK. While framework APIs vary in capability, we can provide consistent tracking for the most important features:

- ✅ **Test order within worker/thread** - Fully supported
- ✅ **Previous test information** - Fully supported per-worker
- ✅ **Parallelization detection** - Supported with varying accuracy
- ✅ **Thread/Worker identification** - Fully supported
- ✅ **Suite timing context** - Fully supported
- ⚠️ **Total test count** - Not reliably available, will be null
- ⚠️ **Global order** - Best effort, approximate in parallel scenarios

This feature will enable detection of:
- Order-dependent test failures
- Parallel execution issues
- "Late in suite" failures
- Worker/thread-specific problems
- Resource contention patterns

**Recommendation**: Proceed with implementation following the phased approach outlined above.
