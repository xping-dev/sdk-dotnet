/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Hosting;
using Xping.Cli.Services;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Commands;

/// <summary>
/// Prints where the local store lives and what it holds.
/// </summary>
/// <remarks>
/// The store's location is discovered by walking up for a repository root, so "where is my data"
/// is not answerable by inspection. This makes it answerable, and doubles as the first thing to run
/// when a report is unexpectedly empty.
/// </remarks>
internal sealed class WhereCommand(ILocalSessionStoreFactory storeFactory, ConsoleIO io)
{
    public int Run(string? directory)
    {
        TextWriter output = io.Output;

        ILocalSessionStore store = storeFactory.Create(
            startDirectory: directory ?? Directory.GetCurrentDirectory());

        if (!store.IsAvailable || store.StorePath == null)
        {
            output.WriteLine("No Xping local store could be resolved from this directory.");
            return 1;
        }

        output.WriteLine(store.StorePath);

        string? sessionsDirectory = store.SessionsPath;
        if (sessionsDirectory == null || !Directory.Exists(sessionsDirectory))
        {
            output.WriteLine("  (no runs recorded yet)");
            return 0;
        }

        var files = new DirectoryInfo(sessionsDirectory).GetFiles("session-*.json.gz");
        long bytes = files.Sum(f => f.Length);

        output.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "  {0} {1} · {2} on disk",
            files.Length,
            files.Length == 1 ? "run" : "runs",
            FormatSize(bytes)));

        // A large window: this is a diagnostic, so completeness beats speed.
        IReadOnlyList<TestSession> sessions = store.ReadRecent(500).Sessions;

        // A run is listed under every assembly it executed, so the run counts below can add up to
        // more than the file count above. That is the honest reading: one `dotnet test` across a
        // solution is one run of each test project it covered, and each of them has that much
        // history. Collapsing it to a single arbitrary assembly is what this listing exists to
        // stop the reader believing.
        var byAssembly = sessions
            .SelectMany(AssemblyPairs)
            .GroupBy(pair => pair.Assembly, pair => pair.Session, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in byAssembly)
        {
            DateTime newest = group.Max(s => s.StartedAt);

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "  {0,-40} {1,4} {2}  last {3:yyyy-MM-dd HH:mm} UTC",
                Truncate(group.Key, 40),
                group.Count(),
                group.Count() == 1 ? "run " : "runs",
                newest));
        }

        return 0;
    }

    /// <summary>
    /// Formats a byte count for a person reading a diagnostic.
    /// </summary>
    /// <remarks>
    /// Switches to megabytes past a thousand kilobytes. A whole session is a large document, so a
    /// populated store reaches four-digit kilobyte figures that nobody reads at a glance.
    /// </remarks>
    private static string FormatSize(long bytes) =>
        bytes >= 1024L * 1024
            ? string.Format(CultureInfo.InvariantCulture, "{0:0.0} MB", bytes / (1024.0 * 1024.0))
            : string.Format(CultureInfo.InvariantCulture, "{0:0.0} KB", bytes / 1024.0);

    /// <summary>
    /// Pairs a session with each test assembly it executed.
    /// </summary>
    /// <remarks>
    /// A session that named no assembly at all is still listed, under <c>(unknown)</c>. It is
    /// invisible to every scoped report — there is nothing to scope it by — so a diagnostic that
    /// dropped it too would leave the runs unaccounted for and the file count unexplained.
    /// </remarks>
    private static IEnumerable<(string Assembly, TestSession Session)> AssemblyPairs(
        TestSession session)
    {
        IReadOnlyList<string> assemblies = SessionAssemblies.Of(session);

        return assemblies.Count == 0
            ? [("(unknown)", session)]
            : assemblies.Select(assembly => (assembly, session));
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : string.Concat(value.AsSpan(0, max - 1), "~");
}
