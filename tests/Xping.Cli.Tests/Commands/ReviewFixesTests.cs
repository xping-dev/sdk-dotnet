/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Commands;
using Xping.Cli.Tests.Report;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.Local;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests.Commands;

// Resolves the store from a temp directory and mutates XPING_NO_BANNER.
[Collection("Sequential")]
public sealed class ReviewFixesTests : IDisposable
{
    private readonly string _root;

    public ReviewFixesTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-reviewfix-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("XPING_LOCAL_STORE", _root);
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XPING_LOCAL_STORE", null);
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, null);

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>
    /// Writes runs to the slim run tier, which is what the cloud invitation reads.
    /// </summary>
    private static void SeedRuns(string assembly, DateTime start, int count, bool connected)
    {
        ILocalRunStore store = LocalRunStore.Create();

        for (int i = 0; i < count; i++)
        {
            store.Write(new LocalRun(
                new LocalRunHeader
                {
                    SessionId = Guid.NewGuid().ToString("N"),
                    StartedAtUtc = start.AddMinutes(i),
                    Assembly = assembly,
                    IsConnected = connected
                },
                [
                    new LocalTestRecord
                    {
                        Fingerprint = $"fp-{assembly}",
                        Name = $"{assembly}.Test",
                        Outcome = OutcomeCodes.Passed
                    }
                ]));
        }
    }

    /// <summary>
    /// Writes sessions in which one test stops running, so the report produces a finding.
    /// </summary>
    /// <remarks>
    /// The cloud invitation is only offered once the developer has been shown a problem worth
    /// solving, so a store with no findings cannot exercise it.
    /// </remarks>
    private static void SeedVanishingSessions()
    {
        ILocalSessionStore store = LocalSessionStore.Create();

        for (int i = 0; i < 8; i++)
        {
            List<TestExecution> executions = i < 5
                ? [TestSessionFactory.Execution("Stable"), TestSessionFactory.Execution("Removed")]
                : [TestSessionFactory.Execution("Stable")];

            store.Write(TestSessionFactory.Session(i, executions));
        }
    }

    private static (int Code, string Output) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Rendered as if a person were watching, so the parts of the output only a terminal gets —
        // the scope notice, the cloud invitation — are exercised rather than silently suppressed.
        int code = Program.Run(args, output, error, input: null, isTerminal: true);
        return (code, output.ToString() + error.ToString());
    }

    // -----------------------------------------------------------------------
    // Destructive command safety
    // -----------------------------------------------------------------------

    [Fact]
    public void ClearRejectsAnAssemblyFlagWithNoValueInsteadOfDeletingEverything()
    {
        // Arrange — `--assembly` with no value used to read as "no scope", turning a scoped delete
        // into a full one. For a destructive command that is the worst possible misparse.
        SeedRuns("Alpha.Tests", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 3, connected: false);

        // Act
        var (code, output) = Run("clear", "--force", "--assembly");

        // Assert
        Assert.Equal(2, code);
        Assert.Contains("Required argument missing", output, StringComparison.Ordinal);
        Assert.Equal(3, LocalRunStore.Create().ReadRecent(100).Count);
    }

    [Fact]
    public void ClearRejectsUnknownOptionsInsteadOfIgnoringThem()
    {
        SeedRuns("Alpha.Tests", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 2, connected: false);

        var (code, output) = Run("clear", "--force", "--dry-run");

        Assert.Equal(2, code);
        Assert.Contains("Unrecognized command or argument", output, StringComparison.Ordinal);
        Assert.Equal(2, LocalRunStore.Create().ReadRecent(100).Count);
    }

    [Fact]
    public void WhereRejectsUnknownOptions()
    {
        var (code, output) = Run("where", "--nonsense");

        Assert.Equal(2, code);
        Assert.Contains("Unrecognized command or argument", output, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Subcommand help
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("report")]
    [InlineData("where")]
    [InlineData("clear")]
    public void SubcommandHelpSucceeds(string verb)
    {
        // Parse errors advertise `xping <verb> --help`, so it has to work rather than being
        // rejected as an unknown option.
        var (code, output) = Run(verb, "--help");

        Assert.Equal(0, code);
        Assert.Contains("Usage:", output, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Cloud invitation
    // -----------------------------------------------------------------------

    [Fact]
    public void CtaIsSuppressedWhenTheStoredRunsCameFromAConnectedProject()
    {
        // Arrange — isConnected was once hard-coded to false, so existing customers were pitched to
        // despite CtaThrottle explicitly excluding them.
        SeedVanishingSessions();
        SeedRuns(
            TestSessionFactory.DefaultAssembly,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            4,
            connected: true);

        // Act
        var (code, output) = Run("report", "--ascii");

        // Assert
        Assert.Equal(0, code);
        Assert.DoesNotContain("xping.io/start", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CtaIsOfferedForLocalOnlyRuns()
    {
        SeedVanishingSessions();
        SeedRuns(
            TestSessionFactory.DefaultAssembly,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            4,
            connected: false);

        var (code, output) = Run("report", "--ascii");

        Assert.Equal(0, code);
        Assert.Contains("xping.io/start", output, StringComparison.Ordinal);
    }
}
