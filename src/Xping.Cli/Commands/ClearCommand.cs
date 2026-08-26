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
/// Deletes recorded runs from the local store.
/// </summary>
/// <remarks>
/// Local history is not recoverable — it is not in version control and is not uploaded in local-only
/// mode — so this confirms before deleting. When stdin is not interactive the command refuses rather
/// than assuming consent, which keeps a stray `xping clear` in a script from silently discarding
/// weeks of history.
/// <para>
/// Scoping with <c>--assembly</c> is exact: one run can hold several test projects' history, and
/// clearing one of them leaves the rest of that run intact.
/// </para>
/// </remarks>
internal sealed class ClearCommand(
    ILocalSessionStoreFactory storeFactory,
    ConsoleIO io)
{
    public int Run(string? directory, string? assembly, bool force)
    {
        TextReader input = io.Input;
        TextWriter output = io.Output;
        TextWriter error = io.Error;

        string startDirectory = directory ?? Directory.GetCurrentDirectory();

        ILocalSessionStore store = storeFactory.Create(startDirectory: startDirectory);

        if (!store.IsAvailable || store.StorePath == null)
        {
            error.WriteLine("No Xping local store could be resolved from this directory.");
            return 1;
        }

        IReadOnlyList<TestSession> matching = store.ReadRecent(int.MaxValue, assembly).Sessions;

        if (matching.Count == 0)
        {
            output.WriteLine(assembly == null
                ? "Nothing to clear."
                : $"No runs recorded for assembly '{assembly}'.");
            return 0;
        }

        string scope = assembly == null
            ? $"all {matching.Count} {(matching.Count == 1 ? "run" : "runs")}"
            : $"{matching.Count} {(matching.Count == 1 ? "run" : "runs")} for '{assembly}'";

        if (!force && !Confirm(scope, store.StorePath, input, output, error))
            return 1;

        int deleted = store.Delete(assembly);

        output.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "Deleted {0} {1}.",
            deleted,
            deleted == 1 ? "run" : "runs"));

        return deleted == matching.Count ? 0 : 1;
    }

    private static bool Confirm(
        string scope, string storePath, TextReader input, TextWriter output, TextWriter error)
    {
        if (Console.IsInputRedirected)
        {
            error.WriteLine(
                $"Refusing to delete {scope} without confirmation. " +
                "Re-run with --force to proceed non-interactively.");
            return false;
        }

        output.Write($"Delete {scope} from {storePath}? [y/N] ");
        output.Flush();

        string? answer = input.ReadLine();

        if (string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            return true;

        output.WriteLine("Cancelled.");
        return false;
    }
}
