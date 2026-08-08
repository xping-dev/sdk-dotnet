/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Sdk.Core.Models.Local;
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
internal static class WhereCommand
{
    public static int Run(string? directory, TextWriter output)
    {
        ILocalRunStore store = LocalRunStore.Create(
            startDirectory: directory ?? Directory.GetCurrentDirectory());

        if (!store.IsAvailable || store.StorePath == null)
        {
            output.WriteLine("No Xping local store could be resolved from this directory.");
            return 1;
        }

        output.WriteLine(store.StorePath);

        string? runsDirectory = store.RunsPath;
        if (runsDirectory == null || !Directory.Exists(runsDirectory))
        {
            output.WriteLine("  (no runs recorded yet)");
            return 0;
        }

        var files = new DirectoryInfo(runsDirectory).GetFiles("run-*.jsonl.gz");
        long bytes = files.Sum(f => f.Length);

        output.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "  {0} {1} · {2:0.0} KB on disk",
            files.Length,
            files.Length == 1 ? "run" : "runs",
            bytes / 1024.0));

        // A large window: this is a diagnostic, so completeness beats speed.
        IReadOnlyList<LocalRun> runs = store.ReadRecent(500);

        var byAssembly = runs
            .GroupBy(r => r.Header.Assembly ?? "(unknown)", StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in byAssembly)
        {
            DateTime newest = group.Max(r => r.Header.StartedAtUtc);

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

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : string.Concat(value.AsSpan(0, max - 1), "~");
}
