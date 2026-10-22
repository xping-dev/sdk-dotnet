/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Time.Testing;
using Xping.Cli.Report;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

public sealed class TestResolverTests
{
    private const string Assembly = TestSessionFactory.DefaultAssembly;

    private const string Other = "Other.Tests";

    // Named after the whole identity so two tests in one session never share an execution id.
    private static TestExecution Test(
        string fullyQualifiedName, string fingerprint, string assembly = Assembly, int attempt = 1) =>
        TestSessionFactory.Execution(
            $"{assembly}|{fingerprint}|{fullyQualifiedName}",
            assembly: assembly,
            attempt: attempt,
            fingerprint: fingerprint,
            fullyQualifiedName: fullyQualifiedName);

    private static TestSession Session(int ordinal, params TestExecution[] executions) =>
        TestSessionFactory.Session(ordinal, executions);

    // Newest first, as the store hands them over.
    private static IReadOnlyList<TestSession> Store(params TestSession[] sessions) =>
        TestSessionFactory.Window(sessions).Sessions;

    private static TestResolution Resolve(string query, params TestExecution[] tests) =>
        TestResolver.Resolve(Store(Session(0, tests)), query, null);

    private static string[] Fingerprints(TestResolution resolution) =>
        [.. resolution.Candidates.Select(c => c.Fingerprint)];

    [Fact]
    public void AnExactNameWinsOverASuffixMatch()
    {
        TestResolution resolution = Resolve(
            "Calc.Add",
            Test("Calc.Add", "aaaaaaaa1"),
            Test("Ns.Calc.Add", "bbbbbbbb1"));

        Assert.Equal(TestResolutionKind.Found, resolution.Kind);
        Assert.Equal("aaaaaaaa1", resolution.Match!.Fingerprint);
    }

    [Theory]
    [InlineData("Add")]
    [InlineData("Calc.Add")]
    [InlineData("Ns.Calc.Add")]
    public void AnyTrailingRunOfSegmentsNamesTheTest(string query)
    {
        TestResolution resolution = Resolve(query, Test("Ns.Calc.Add", "aaaaaaaa1"));

        Assert.Equal(TestResolutionKind.Found, resolution.Kind);
    }

    [Theory]
    [InlineData("dd")]
    [InlineData("alc.Add")]
    [InlineData("add")]
    public void APartialSegmentOrADifferentCaseDoesNotMatch(string query)
    {
        TestResolution resolution = Resolve(query, Test("Ns.Calc.Add", "aaaaaaaa1"));

        Assert.Equal(TestResolutionKind.NotFound, resolution.Kind);
    }

    [Fact]
    public void ABareMethodNameMatchesEveryParameterisedCase()
    {
        TestResolution resolution = Resolve(
            "Add",
            Test("Ns.Calc.Add(1,2)", "aaaaaaaa1"),
            Test("Ns.Calc.Add(2,3)", "bbbbbbbb1"));

        Assert.Equal(TestResolutionKind.Ambiguous, resolution.Kind);
        Assert.Equal(["aaaaaaaa1", "bbbbbbbb1"], Fingerprints(resolution));
    }

    [Theory]
    [InlineData("Add(1,2)", "Ns.Calc.Add(1,2)")]
    [InlineData("Calc.Add(a: 1, b: 2)", "Ns.Calc.Add(a: 1, b: 2)")]
    [InlineData("Add(System.Int32,System.Int32)", "Ns.Calc.Add(System.Int32,System.Int32)")]
    [InlineData("Add(\"(x)\")", "Ns.Calc.Add(\"(x)\")")]
    public void ANameWithArgumentsNamesOneCase(string query, string fullyQualifiedName)
    {
        TestResolution resolution = Resolve(
            query,
            Test(fullyQualifiedName, "aaaaaaaa1"),
            Test("Ns.Calc.Add(9,9)", "bbbbbbbb1"));

        Assert.Equal(TestResolutionKind.Found, resolution.Kind);
        Assert.Equal("aaaaaaaa1", resolution.Match!.Fingerprint);
    }

    [Fact]
    public void APinShorterThanEightCharactersDoesNotPrefixItsCases()
    {
        TestResolution resolution = Resolve(
            "login",
            Test("Ns.Auth.SignsIn(\"a\")", "login:3f2a11"),
            Test("Ns.Auth.SignsIn(\"b\")", "login:9c0d22"));

        Assert.Equal(TestResolutionKind.NotFound, resolution.Kind);
    }

    [Fact]
    public void TheFullNameWithoutArgumentsMatchesEveryCase()
    {
        TestResolution resolution = Resolve(
            "Ns.Calc.Add",
            Test("Ns.Calc.Add(1,2)", "aaaaaaaa1"),
            Test("Ns.Calc.Add(2,3)", "bbbbbbbb1"),
            Test("Ns.Calc.AddMore", "cccccccc1"));

        Assert.Equal(["aaaaaaaa1", "bbbbbbbb1"], Fingerprints(resolution));
    }

    [Fact]
    public void ArgumentsThatHoldParenthesesAreRemovedWhole()
    {
        TestResolution resolution = Resolve("Add", Test("Ns.Calc.Add(\"(x)\", Max(1))", "aaaaaaaa1"));

        Assert.Equal(TestResolutionKind.Found, resolution.Kind);
    }

    [Fact]
    public void AParameterisedFixtureKeepsItsArgumentsWhenTheMethodsAreRemoved()
    {
        TestResolution resolution = Resolve(
            "Fixture(1).Add",
            Test("Ns.Fixture(1).Add(2)", "aaaaaaaa1"),
            Test("Ns.Fixture(2).Add(2)", "bbbbbbbb1"));

        Assert.Equal(TestResolutionKind.Found, resolution.Kind);
        Assert.Equal("aaaaaaaa1", resolution.Match!.Fingerprint);
    }

    [Theory]
    [InlineData("Inner.Method")]
    [InlineData("Outer.Inner.Method")]
    [InlineData("Outer+Inner.Method")]
    [InlineData("Ns.Outer.Inner.Method")]
    public void ANestedTypeMatchesWithADotOrAPlus(string query)
    {
        TestResolution resolution = Resolve(query, Test("Ns.Outer+Inner.Method", "aaaaaaaa1"));

        Assert.Equal(TestResolutionKind.Found, resolution.Kind);
    }

    [Theory]
    [InlineData("Method", true)]
    [InlineData("Ns.Generic`1.Method", true)]
    [InlineData("Generic.Method", false)]
    public void GenericArityIsNotNormalised(string query, bool found)
    {
        TestResolution resolution = Resolve(query, Test("Ns.Generic`1.Method", "aaaaaaaa1"));

        Assert.Equal(found ? TestResolutionKind.Found : TestResolutionKind.NotFound, resolution.Kind);
    }

    [Fact]
    public void AnEightCharacterFingerprintPrefixNamesTheTest()
    {
        TestResolution resolution = Resolve("3a597eb2", Test("Ns.Calc.Add", "3a597eb2c0ffee"));

        Assert.Equal(TestResolutionKind.Found, resolution.Kind);
    }

    [Fact]
    public void AFingerprintPrefixShorterThanEightCharactersDoesNotMatch()
    {
        TestResolution resolution = Resolve("3a597eb", Test("Ns.Calc.Add", "3a597eb2c0ffee"));

        Assert.Equal(TestResolutionKind.NotFound, resolution.Kind);
    }

    [Fact]
    public void AWholePinnedFingerprintShorterThanEightCharactersMatches()
    {
        TestResolution resolution = Resolve("login", Test("Ns.Auth.SignsIn", "login"));

        Assert.Equal(TestResolutionKind.Found, resolution.Kind);
        Assert.Equal("login", resolution.Match!.ShortFingerprint);
    }

    [Fact]
    public void AnUpperCaseHexPrefixIsRetriedInLowerCase()
    {
        TestResolution resolution = Resolve("3A597EB2", Test("Ns.Calc.Add", "3a597eb2c0ffee"));

        Assert.Equal(TestResolutionKind.Found, resolution.Kind);
    }

    [Fact]
    public void APinnedFingerprintIsNotLowerCased()
    {
        TestResolution resolution = Resolve("LOGIN_FLOW", Test("Ns.Auth.SignsIn", "login_flow"));

        Assert.Equal(TestResolutionKind.NotFound, resolution.Kind);
    }

    [Fact]
    public void EachParameterisedCaseOfAPinHasAShortFingerprintPastTheColon()
    {
        TestResolution resolution = Resolve(
            "SignsIn",
            Test("Ns.Auth.SignsIn(\"a\")", "login:3f2a11"),
            Test("Ns.Auth.SignsIn(\"b\")", "login:9c0d22"));

        Assert.Equal(TestResolutionKind.Ambiguous, resolution.Kind);
        Assert.Equal(["login:3f", "login:9c"], resolution.Candidates.Select(c => c.ShortFingerprint));
    }

    /// <summary>
    /// A view that only names a test prints the prefix <c>--test</c> would give it as a candidate.
    /// </summary>
    [Fact]
    public void AShortFingerprintIsTheOneItsCandidateCarries()
    {
        IReadOnlyList<TestSession> store = Store(Session(
            0,
            Test("Ns.Auth.SignsIn(\"a\")", "login:3f2a11"),
            Test("Ns.Auth.SignsIn(\"b\")", "login:9c0d22"),
            Test("Ns.Auth.SignsOut", "login:3f2a11", Other)));

        TestResolution resolution = TestResolver.Resolve(store, "SignsIn", Assembly);

        foreach (TestMatch candidate in resolution.Candidates)
            Assert.Equal(candidate.ShortFingerprint, TestResolver.ShortFingerprintOf(store, Assembly, candidate.Fingerprint));
    }

    [Fact]
    public void ATestThatDidNotRunInTheAssemblyHasNoShortFingerprint()
    {
        IReadOnlyList<TestSession> store = Store(Session(0, Test("Ns.Auth.SignsOut", "login:3f2a11", Other)));

        Assert.Null(TestResolver.ShortFingerprintOf(store, Assembly, "login:3f2a11"));
    }

    [Fact]
    public void AShortFingerprintOfAPinnedCaseNamesThatCaseAlone()
    {
        TestResolution resolution = Resolve(
            "login:3f",
            Test("Ns.Auth.SignsIn(\"a\")", "login:3f2a11"),
            Test("Ns.Auth.SignsIn(\"b\")", "login:9c0d22"));

        Assert.Equal(TestResolutionKind.Found, resolution.Kind);
        Assert.Equal("login:3f2a11", resolution.Match!.Fingerprint);
    }

    [Fact]
    public void AWholeFingerprintNamesItsTestEvenWhenItIsAPrefixOfAnother()
    {
        TestResolution resolution = Resolve(
            "login",
            Test("Ns.Auth.SignsIn", "login"),
            Test("Ns.Auth.SignsIn(\"a\")", "login:3f2a11"));

        Assert.Equal(TestResolutionKind.Found, resolution.Kind);
        Assert.Equal("login", resolution.Match!.Fingerprint);
    }

    [Fact]
    public void AFingerprintThatPrefixesAnotherHasItsWholeFingerprintAsItsShortOne()
    {
        TestResolution resolution = Resolve(
            "SignsIn",
            Test("Ns.Auth.SignsIn", "abcdefgh"),
            Test("Ns.Other.SignsIn", "abcdefgh12"));

        Assert.Equal(["abcdefgh", "abcdefgh1"], resolution.Candidates.Select(c => c.ShortFingerprint));
    }

    [Fact]
    public void AHexLookingMethodNameIsMatchedByNameBeforeAnyFingerprint()
    {
        TestResolution resolution = Resolve(
            "deadbeef",
            Test("Ns.Codes.deadbeef", "11111111aa"),
            Test("Ns.Calc.Add", "deadbeef22"));

        Assert.Equal(TestResolutionKind.Found, resolution.Kind);
        Assert.Equal("11111111aa", resolution.Match!.Fingerprint);
    }

    [Fact]
    public void AShortFingerprintThatIsAlsoAMethodNameWidensUntilItNamesItsOwnTest()
    {
        TestExecution[] tests =
        [
            Test("Ns.Codes.deadbeef", "11111111aa"),
            Test("Ns.Calc.Add", "deadbeef22")
        ];

        TestMatch add = Resolve("Add", tests).Match!;
        TestResolution again = Resolve(add.ShortFingerprint, tests);

        Assert.Equal("deadbeef2", add.ShortFingerprint);
        Assert.Equal("deadbeef22", again.Match!.Fingerprint);
    }

    [Fact]
    public void AShortUpperCaseQueryDoesNotMatchALowerCasePin()
    {
        TestResolution resolution = Resolve("CAFE", Test("Ns.Shop.Pays", "cafe"));

        Assert.Equal(TestResolutionKind.NotFound, resolution.Kind);
    }

    [Theory]
    [InlineData("Ns.T.Smile(\":)\")")]
    [InlineData("Ns.T.Smile(\"(\")")]
    [InlineData("Ns.T.Smile('(')")]
    [InlineData("Ns.T.Smile(\"\\\")\")")]
    public void AParenthesisInsideALiteralDoesNotHideTheArgumentList(string fullyQualifiedName)
    {
        TestResolution resolution = Resolve(
            "Smile",
            Test(fullyQualifiedName, "aaaaaaaa1"),
            Test("Ns.T.Smile(\"a\")", "bbbbbbbb1"));

        Assert.Equal(TestResolutionKind.Ambiguous, resolution.Kind);
    }

    [Fact]
    public void APlusInsideTheArgumentsIsNotReadAsADot()
    {
        TestResolution resolution = Resolve(
            "Concat(\"a.b\")",
            Test("Ns.S.Concat(\"a+b\")", "aaaaaaaa1"),
            Test("Ns.S.Concat(\"a.b\")", "bbbbbbbb1"));

        Assert.Equal(TestResolutionKind.Found, resolution.Kind);
        Assert.Equal("bbbbbbbb1", resolution.Match!.Fingerprint);
    }

    [Fact]
    public void AnAssemblyGivenWithAnUnscopedReadStillRestrictsTheLookup()
    {
        IReadOnlyList<TestSession> store = Store(
            Session(0, Test("Ns.A.Foo", "aaaaaaaa1", Other)),
            Session(1, Test("Ns.A.Foo", "bbbbbbbb1"), Test("Ns.A.Bar", "cccccccc1", Other)),
            Session(2, Test("Ns.A.Bar", "dddddddd1", Other)));

        TestResolution found = TestResolver.Resolve(store, "Foo", Assembly);
        TestResolution missing = TestResolver.Resolve(store, "Nope", Assembly);

        Assert.Equal("bbbbbbbb1", found.Match!.Fingerprint);
        Assert.Equal(1, missing.RunCount);
        Assert.Equal(1, missing.AssemblyCount);
    }

    [Fact]
    public void ARenamedTestIsFoundUnderItsNewestNameOnly()
    {
        IReadOnlyList<TestSession> store = Store(
            Session(0, Test("Ns.Calc.OldName", "aaaaaaaa1")),
            Session(1, Test("Ns.Calc.NewName", "aaaaaaaa1")));

        TestResolution byNew = TestResolver.Resolve(store, "NewName", null);
        TestResolution byOld = TestResolver.Resolve(store, "OldName", null);

        Assert.Equal(TestResolutionKind.Found, byNew.Kind);
        Assert.Equal("Ns.Calc.NewName", byNew.Match!.FullyQualifiedName);
        Assert.Equal(2, byNew.Match.Runs);
        Assert.Equal(TestResolutionKind.NotFound, byOld.Kind);
    }

    [Fact]
    public void TheSameFingerprintInTwoAssembliesIsTwoTests()
    {
        TestResolution resolution = Resolve(
            "login",
            Test("Ns.Auth.SignsIn", "login"),
            Test("Ns.Auth.SignsIn", "login", Other));

        Assert.Equal(TestResolutionKind.Ambiguous, resolution.Kind);
        Assert.Equal([Assembly, Other], resolution.Candidates.Select(c => c.Assembly).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void CandidatesAreListedNewestRunFirst()
    {
        IReadOnlyList<TestSession> store = Store(
            Session(0, Test("Ns.A.Add", "aaaaaaaa1"), Test("Ns.B.Add", "bbbbbbbb1")),
            Session(1, Test("Ns.B.Add", "bbbbbbbb1")),
            Session(2, Test("Ns.C.Add", "cccccccc1")));

        TestResolution resolution = TestResolver.Resolve(store, "Add", null);

        Assert.Equal(["cccccccc1", "bbbbbbbb1", "aaaaaaaa1"], Fingerprints(resolution));
        Assert.Equal(TestSessionFactory.Epoch.AddMinutes(1), resolution.Candidates[1].LastSeen);
    }

    [Fact]
    public void AShortFingerprintWidensPastEightCharactersWhenTwoShareThem()
    {
        TestResolution resolution = Resolve(
            "Add",
            Test("Ns.A.Add", "3a597eb2aa"),
            Test("Ns.B.Add", "3a597eb2bb"),
            Test("Ns.C.Add", "91c0de44cc"));

        Assert.Equal(
            ["3a597eb2a", "3a597eb2b", "91c0de44"],
            resolution.Candidates.Select(c => c.ShortFingerprint));
    }

    [Fact]
    public void AShortFingerprintIsUniqueOnlyWithinItsAssembly()
    {
        TestResolution resolution = Resolve(
            "Add",
            Test("Ns.A.Add", "3a597eb2aa"),
            Test("Ns.B.Add", "3a597eb2bb", Other));

        Assert.All(resolution.Candidates, c => Assert.Equal("3a597eb2", c.ShortFingerprint));
    }

    [Fact]
    public void RetriesInOneSessionCountAsOneRun()
    {
        TestResolution resolution = Resolve(
            "Add",
            Test("Ns.Calc.Add", "aaaaaaaa1"),
            Test("Ns.Calc.Add", "aaaaaaaa1", attempt: 2));

        Assert.Equal(1, resolution.Match!.Runs);
    }

    [Fact]
    public void NotFoundNamesTheStoreItLookedIn()
    {
        IReadOnlyList<TestSession> store = Store(
            Session(0, Test("Ns.A.Add", "aaaaaaaa1")),
            Session(1, Test("Ns.B.Sub", "bbbbbbbb1", Other)));

        TestResolution resolution = TestResolver.Resolve(store, "Ad", null);

        Assert.Equal(TestResolutionKind.NotFound, resolution.Kind);
        Assert.Empty(resolution.Candidates);
        Assert.Equal(
            "No test matching 'Ad' in this store (2 assemblies, 2 runs).",
            TestResolver.NotFoundMessage(resolution));
    }

    [Fact]
    public void NotFoundCountsInTheSingular()
    {
        TestResolution resolution = Resolve("Ad", Test("Ns.A.Add", "aaaaaaaa1"));

        Assert.Equal(
            "No test matching 'Ad' in this store (1 assembly, 1 run).",
            TestResolver.NotFoundMessage(resolution));
    }

    [Fact]
    public void NotFoundNamesTheAssemblyWhenOneWasGiven()
    {
        IReadOnlyList<TestSession> store = Store(
            Session(0, Test("Ns.A.Add", "aaaaaaaa1")),
            Session(1, Test("Ns.A.Add", "aaaaaaaa1")));

        TestResolution resolution = TestResolver.Resolve(store, "Foo", Assembly);

        Assert.Equal(
            $"No test matching 'Foo' in {Assembly} (2 runs).",
            TestResolver.NotFoundMessage(resolution));
    }

    [Fact]
    public void ATestOutsideARunsWindowSaysWhichFlagHidItAndHowManyRunsItHas()
    {
        TestSession[] sessions =
        [
            Session(0, Test("Ns.A.Foo", "aaaaaaaa1")),
            Session(1, Test("Ns.A.Foo", "aaaaaaaa1")),
            Session(2, Test("Ns.A.Bar", "bbbbbbbb1"))
        ];
        TestMatch match = TestResolver.Resolve(Store(sessions), "Foo", null).Match!;
        AnalysisWindow window = AnalysisWindow.Create(
            [sessions[2]], sessions[2].StartedAt, sessions[2].StartedAt, WindowResolution.Runs, "1");

        Assert.False(TestResolver.InWindow(match, window));
        Assert.Equal(
            "No run of 'Foo' in this window (--runs 1). It ran in 2 earlier runs; drop --runs to see them.",
            TestResolver.OutsideWindowMessage("Foo", match, window));
    }

    [Theory]
    [InlineData(nameof(WindowResolution.SinceDate), "2026-08-02")]
    [InlineData(nameof(WindowResolution.SinceSha), "c670fc3")]
    public void ATestOutsideASinceWindowNamesSince(string since, string argument)
    {
        WindowResolution resolution = Enum.Parse<WindowResolution>(since);
        TestSession old = Session(0, Test("Ns.A.Foo", "aaaaaaaa1"));
        TestSession recent = Session(1, Test("Ns.A.Bar", "bbbbbbbb1"));
        TestMatch match = TestResolver.Resolve(Store(old, recent), "Foo", null).Match!;
        AnalysisWindow window = AnalysisWindow.Create(
            [recent], recent.StartedAt, recent.StartedAt, resolution, argument);

        Assert.Equal(
            $"No run of 'Foo' in this window (--since {argument}). It ran in 1 earlier run; drop --since to see them.",
            TestResolver.OutsideWindowMessage("Foo", match, window));
    }

    [Fact]
    public void ATestInTheWindowIsInIt()
    {
        TestSession session = Session(0, Test("Ns.A.Foo", "aaaaaaaa1"));
        TestMatch match = TestResolver.Resolve(Store(session), "Foo", null).Match!;

        Assert.True(TestResolver.InWindow(match, TestSessionFactory.Window(session)));
    }

    [Fact]
    public void TheLookupReadsEveryAssemblyWhenNoneIsGiven()
    {
        var source = new FakeSessionSource(
            Session(0, Test("Ns.A.Foo", "aaaaaaaa1", Other)),
            Session(1, Test("Ns.A.Bar", "bbbbbbbb1")));

        TestLookup lookup = TestResolver.Resolve(Resolver(), source, "Foo", null);

        Assert.Equal(TestResolutionKind.Found, lookup.Resolution!.Kind);
        Assert.Equal(Other, lookup.Resolution.Match!.Assembly);
        Assert.Null(lookup.Read.Window);
    }

    [Fact]
    public void TheUnscopedLookupNeverReadsTheStoreUnscoped()
    {
        var source = new FakeSessionSource(
            Session(0, Test("Ns.A.Foo", "aaaaaaaa1", Other)),
            Session(1, Test("Ns.A.Bar", "bbbbbbbb1")));

        TestResolver.Resolve(Resolver(), source, "Foo", null);

        Assert.Equal([Assembly, Other], source.Reads.Select(r => r.Assembly));
    }

    [Fact]
    public void ALookupFindsATestWhoseOnlyRunsAreOlderThanTheThousandthSession()
    {
        // 21 suites at 50 runs each: one more than a 1000-session read held. Foo's suite ran first,
        // so its whole history sat past that read and the name was reported as never run (#283).
        const int Suites = 21;
        const int RunsEach = 50;

        TestSession[] sessions =
        [
            .. Enumerable.Range(0, Suites * RunsEach).Select(k =>
            {
                int suite = k / RunsEach;
                TestExecution test = suite == 0
                    ? Test("Ns.A.Foo", "aaaaaaaa1", "Suite00.Tests")
                    : Test($"Ns.S{suite}.Bar", $"bbbbbb{suite:D2}", $"Suite{suite:D2}.Tests");

                return TestSessionFactory.Session(k, [test], startedAt: TestSessionFactory.Epoch.AddMinutes(-2000 + k));
            })
        ];

        TestLookup lookup = TestResolver.Resolve(Resolver(), new FakeSessionSource(sessions), "Foo", null);

        Assert.Equal(TestResolutionKind.Found, lookup.Resolution!.Kind);
        Assert.Equal("Suite00.Tests", lookup.Resolution.Match!.Assembly);
        Assert.Equal(RunsEach, lookup.Resolution.Match.Runs);
        Assert.Equal(Suites * RunsEach, lookup.Resolution.RunCount);
    }

    [Fact]
    public void CandidatesFromDifferentAssembliesAreOrderedNewestRunFirst()
    {
        var source = new FakeSessionSource(
            Session(0, Test("Ns.A.Foo", "aaaaaaaa1")),
            Session(1, Test("Ns.B.Foo", "bbbbbbbb1", Other)),
            Session(2, Test("Ns.C.Foo", "cccccccc1"), Test("Ns.D.Foo", "dddddddd1", Other)));

        TestLookup lookup = TestResolver.Resolve(Resolver(), source, "Foo", null);

        // The newest run covers both suites, so its two tests tie on time and fall to assembly order.
        Assert.Equal(
            ["Ns.C.Foo", "Ns.D.Foo", "Ns.B.Foo", "Ns.A.Foo"],
            lookup.Resolution!.Candidates.Select(c => c.FullyQualifiedName));
    }

    [Fact]
    public void RunCountCountsARunCoveringTwoAssembliesOnce()
    {
        var source = new FakeSessionSource(
            Session(0, Test("Ns.A.Bar", "bbbbbbbb1"), Test("Ns.B.Bar", "bbbbbbbb2", Other)),
            Session(1, Test("Ns.A.Bar", "bbbbbbbb1")));

        TestLookup lookup = TestResolver.Resolve(Resolver(), source, "Foo", null);

        Assert.Equal(2, lookup.Resolution!.RunCount);
        Assert.Equal(2, lookup.Resolution.AssemblyCount);
    }

    [Fact]
    public void AnUnreadableFileIsCountedOnceAcrossAssemblies()
    {
        // A file whose name carries no assembly is opened by every suite's read.
        var source = new FakeSessionSource(
            Session(0, Test("Ns.A.Foo", "aaaaaaaa1")),
            Session(1, Test("Ns.A.Bar", "bbbbbbbb1", Other)))
        {
            UnreadableFiles = ["session-damaged.json.gz"]
        };

        TestLookup lookup = TestResolver.Resolve(Resolver(), source, "Foo", null);

        Assert.Equal(1, lookup.Read.UnreadableSessions);
    }

    [Fact]
    public void ARunAheadOfTheClockCoveringTwoAssembliesIsCountedOnce()
    {
        var source = new FakeSessionSource(
            Session(0, Test("Ns.A.Bar", "bbbbbbbb1")),
            Session(1, Test("Ns.B.Bar", "bbbbbbbb2", Other)),
            TestSessionFactory.Session(
                100,
                [Test("Ns.A.Foo", "aaaaaaaa1"), Test("Ns.B.Foo", "aaaaaaaa2", Other)],
                startedAt: TestSessionFactory.Epoch.AddDays(4)));

        TestLookup lookup = TestResolver.Resolve(Resolver(), source, "Foo", null);

        Assert.Equal(1, lookup.Read.SkewedSessions);
        Assert.False(lookup.Read.WholeStoreSkewed);
        Assert.Equal(TestResolutionKind.NotFound, lookup.Resolution!.Kind);
    }

    [Fact]
    public void AnAssemblyWhoseEveryRunIsAheadIsStillSearched()
    {
        // Other's only run is ahead of the clock. Its read keeps it, as --assembly Other would, so the
        // name resolves the same with or without the assembly.
        var source = new FakeSessionSource(
            Session(0, Test("Ns.A.Bar", "bbbbbbbb1")),
            TestSessionFactory.Session(
                100, [Test("Ns.B.Foo", "aaaaaaaa1", Other)], startedAt: TestSessionFactory.Epoch.AddDays(4)));

        TestLookup lookup = TestResolver.Resolve(Resolver(), source, "Foo", null);

        Assert.Equal(Other, lookup.Resolution!.Match!.Assembly);
        Assert.Equal(0, lookup.Read.SkewedSessions);
        Assert.False(lookup.Read.WholeStoreSkewed);
    }

    [Fact]
    public void AStoreOfUnattributedRunsResolvesToNoAssembly()
    {
        var source = new FakeSessionSource(Session(0, Test("Ns.A.Foo", "aaaaaaaa1", "")));

        TestLookup lookup = TestResolver.Resolve(Resolver(), source, "Foo", null);

        Assert.Null(lookup.Read.Failure);
        Assert.Equal(0, lookup.Resolution!.AssemblyCount);
    }

    [Fact]
    public void AnEmptyStoreFailsAsEmpty()
    {
        TestLookup lookup = TestResolver.Resolve(Resolver(), new FakeSessionSource(), "Foo", null);

        Assert.Equal(WindowFailure.EmptyStore, lookup.Read.Failure);
        Assert.Null(lookup.Resolution);
    }

    [Fact]
    public void TheLookupStaysInTheAssemblyItWasGiven()
    {
        var source = new FakeSessionSource(
            Session(0, Test("Ns.A.Foo", "aaaaaaaa1", Other)),
            Session(1, Test("Ns.A.Bar", "bbbbbbbb1")));

        TestLookup lookup = TestResolver.Resolve(Resolver(), source, "Foo", Assembly);

        Assert.Equal(TestResolutionKind.NotFound, lookup.Resolution!.Kind);
        Assert.Equal(1, lookup.Resolution.RunCount);
    }

    [Fact]
    public void ASessionStampedAheadOfTheClockCannotNameTheTest()
    {
        var source = new FakeSessionSource(
            Session(0, Test("Ns.A.Bar", "bbbbbbbb1")),
            TestSessionFactory.Session(
                1, [Test("Ns.A.Foo", "aaaaaaaa1")], startedAt: TestSessionFactory.Epoch.AddDays(4)));

        TestLookup lookup = TestResolver.Resolve(Resolver(), source, "Foo", null);

        Assert.Equal(1, lookup.Read.SkewedSessions);
        Assert.Equal(TestResolutionKind.NotFound, lookup.Resolution!.Kind);
    }

    [Fact]
    public void AMissingStoreReturnsTheReadFailureAndNoResolution()
    {
        var source = new FakeSessionSource { IsAvailable = false };

        TestLookup lookup = TestResolver.Resolve(Resolver(), source, "Foo", null);

        Assert.Equal(WindowFailure.NoStore, lookup.Read.Failure);
        Assert.Null(lookup.Resolution);
    }

    private static WindowResolver Resolver() =>
        new(new FakeTimeProvider(new DateTimeOffset(TestSessionFactory.Epoch.AddHours(1), TimeSpan.Zero)));
}
