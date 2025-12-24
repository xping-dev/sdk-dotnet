# Known Limitations

This document outlines known limitations and edge cases in the Xping SDK. Understanding these constraints will help you make informed decisions when implementing test observability in your projects.

## Framework-Specific Limitations

### NUnit

#### Ignored Tests Are Not Tracked

**Affected Versions**: All versions
**Impact**: Tests marked with `[Ignore]` attribute will not appear in Xping reporting

**Reason**: The `XpingTrackAttribute` uses NUnit's `ITestAction` interface, which only intercepts tests during the execution phase. Tests with the `[Ignore]` attribute are filtered out by NUnit before execution begins, so the tracking hooks (`BeforeTest` and `AfterTest`) are never invoked.

**Note**: Currently, there is no workaround for tracking ignored tests in NUnit. Only tests that actually execute will be tracked by Xping.

---

### MSTest

#### Ignored Tests Are Not Tracked

**Affected Versions**: All versions
**Impact**: Tests marked with `[Ignore]` attribute will not appear in Xping reporting

**Reason**: The `XpingTestBase` class uses `[TestInitialize]` and `[TestCleanup]` lifecycle hooks, which only execute for tests that run. Tests with the `[Ignore]` attribute are skipped by MSTest before these hooks are invoked.

**Note**: Currently, there is no workaround for tracking ignored tests in MSTest. Only tests that actually execute will be tracked by Xping.

---

### xUnit

**No known limitations** for skipped test tracking.

Tests marked with the `Skip` parameter are properly tracked by Xping:

```csharp
[Fact(Skip = "Temporarily disabled - ticket #123")]
public void MyTest()
{
    // This test WILL be tracked with Outcome = Skipped
}
```

The xUnit adapter uses a message sink pattern that intercepts all test lifecycle events, including skipped tests.

---

## General Limitations

_This section will be updated as general limitations across all frameworks are discovered._

---

## Reporting Issues

If you encounter a limitation not documented here, please:

1. **Search existing issues**: [GitHub Issues](https://github.com/xping-io/sdk-dotnet/issues)
2. **Report new limitations**: [Create New Issue](https://github.com/xping-io/sdk-dotnet/issues/new)

When reporting, please include:
- Test framework and version
- Xping SDK version
- Minimal reproduction code
- Expected vs actual behavior

---

## Version History

| Version | Changes |
|---------|---------|
| 1.0.0   | Initial documentation - NUnit and MSTest `[Ignore]` limitation |
