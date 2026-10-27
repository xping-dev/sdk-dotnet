/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using Xping.Sdk.Core.Attributes;
using xRetry.v3;

// Instance members and public types are what xUnit discovers; making them static or internal, as the
// analyzers would, would make them no longer tests.
[assembly: SuppressMessage("Performance", "CA1822", Justification = "xUnit runs instance test methods.")]
[assembly: SuppressMessage("Design", "CA1515", Justification = "xUnit discovers public test classes only.")]
// xUnit's own analyzer forbids ConfigureAwait(false) in tests, which must keep xUnit's context.
[assembly: SuppressMessage("Reliability", "CA2007", Justification = "Tests keep xUnit's synchronization context.")]

namespace Xping.Sdk.XUnit.V3.Probe;

/// <summary>Why the probe's types are left out of coverage.</summary>
internal static class ProbeCoverage
{
    /// <summary>
    /// The probe's tests fail on purpose and only exist to drive the adapter, so many of their lines
    /// never run. Counted, they would read as missing coverage of the adapter's change.
    /// </summary>
    public const string Justification = "Test input for the adapter's tests, not code under test.";
}

/// <summary>Plain outcomes.</summary>
[ExcludeFromCodeCoverage(Justification = ProbeCoverage.Justification)]
public class OutcomeTests
{
    [Fact]
    public void Passes() => TestContext.Current.TestOutputHelper?.WriteLine("written by the test");

    [Fact]
    public void FailsAnAssertion() => Assert.Equal(1, 2);

    [Fact]
    public void Throws() => throw new InvalidOperationException("thrown by the test body");

    [Fact(Timeout = 100)]
    public async Task TimesOut() => await Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

    [Fact(Skip = "skipped by its attribute")]
    public void SkippedStatically()
    {
    }

    [Fact]
    public void SkippedWhileRunning() => Assert.Skip("skipped while running");

    [Fact(Explicit = true)]
    public void RunsOnlyWhenAskedFor()
    {
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Theory(int value) => Assert.True(value > 0);

    [Fact]
    [Trait("Category", "Fast")]
    [Trait("Description", "described by a trait")]
    [Trait("Owner", "checkout")]
    public void HasTraits()
    {
    }

    [Fact]
    [XpingFingerprint("pinned-fingerprint")]
    public void HasAPinnedFingerprint()
    {
    }
}

/// <summary>Fails its first attempt and passes its second, under xRetry.</summary>
[ExcludeFromCodeCoverage(Justification = ProbeCoverage.Justification)]
public class RetryTests
{
    private static int _attempts;

    /// <summary>Resets the attempt count, so each run starts from a first attempt.</summary>
    public static void Reset() => Interlocked.Exchange(ref _attempts, 0);

    [RetryFact(3)]
    public void PassesOnItsSecondAttempt() =>
        Assert.True(Interlocked.Increment(ref _attempts) >= 2, "the first attempt fails on purpose");
}

/// <summary>A class fixture whose constructor throws.</summary>
[ExcludeFromCodeCoverage(Justification = ProbeCoverage.Justification)]
public sealed class BrokenClassFixture
{
    public BrokenClassFixture() => throw new InvalidOperationException("the class fixture is broken");
}

[ExcludeFromCodeCoverage(Justification = ProbeCoverage.Justification)]
public class ClassFixtureTests(BrokenClassFixture fixture) : IClassFixture<BrokenClassFixture>
{
    [Fact]
    public void NeedsTheFixture() => Assert.NotNull(fixture);
}

/// <summary>A collection fixture whose constructor throws.</summary>
[ExcludeFromCodeCoverage(Justification = ProbeCoverage.Justification)]
public sealed class BrokenCollectionFixture
{
    public BrokenCollectionFixture() => throw new InvalidOperationException("the collection fixture is broken");
}

[CollectionDefinition("Broken collection")]
[ExcludeFromCodeCoverage(Justification = ProbeCoverage.Justification)]
public class BrokenCollection : ICollectionFixture<BrokenCollectionFixture>;

[Collection("Broken collection")]
[ExcludeFromCodeCoverage(Justification = ProbeCoverage.Justification)]
public class CollectionFixtureTests
{
    [Fact]
    public void NeedsTheCollection()
    {
    }
}

[ExcludeFromCodeCoverage(Justification = ProbeCoverage.Justification)]
public class ConstructorThrows
{
    public ConstructorThrows() => throw new InvalidOperationException("the test class constructor throws");

    [Fact]
    public void Test()
    {
    }
}

[ExcludeFromCodeCoverage(Justification = ProbeCoverage.Justification)]
public sealed class DisposeThrows : IDisposable
{
    [SuppressMessage("Usage", "CA1065", Justification = "A Dispose that throws is the case under test.")]
    public void Dispose() => throw new InvalidOperationException("Dispose throws");

    [Fact]
    public void Test()
    {
    }
}

[ExcludeFromCodeCoverage(Justification = ProbeCoverage.Justification)]
public class InitializeAsyncThrows : IAsyncLifetime
{
    public ValueTask InitializeAsync() => throw new InvalidOperationException("InitializeAsync throws");

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return default;
    }

    [Fact]
    public void Test()
    {
    }
}
