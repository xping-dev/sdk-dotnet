/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Hosting;
using Xping.Cli.Services;
using Xping.Sdk.Core.Models.Local;
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
/// </remarks>
internal sealed class ClearCommand(
    ILocalRunStoreFactory storeFactory,
    ILocalSessionStoreFactory sessionStoreFactory,
    ConsoleIO io)
{
    public int Run(string? directory, string? assembly, bool force)
    {
        TextReader input = io.Input;
        TextWriter output = io.Output;
        TextWriter error = io.Error;

        string startDirectory = directory ?? Directory.GetCurrentDirectory();

        ILocalRunStore store = storeFactory.Create(startDirectory: startDirectory);

        if (!store.IsAvailable || store.StorePath == null)
        {
            error.WriteLine("No Xping local store could be resolved from this directory.");
            return 1;
        }

        ILocalSessionStore sessionStore = sessionStoreFactory.Create(startDirectory: startDirectory);

        IReadOnlyList<LocalRun> matching = store.ReadRecent(int.MaxValue, assembly);

        // Emptiness is judged across both tiers. A store holding sessions but no runs is unusual —
        // they are written together — but reporting "nothing to clear" while `xping report` still
        // has history to show would be a straightforward lie.
        if (matching.Count == 0 && sessionStore.ReadRecent(1, assembly).Sessions.Count == 0)
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

        // Both tiers, always. The run tier is what the count above describes, but the session tier is
        // what `xping report` reads — clearing one and leaving the other would show the user history
        // they were just told had been deleted.
        sessionStore.Delete(assembly);

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
