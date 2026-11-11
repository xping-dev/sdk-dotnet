/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth.Store;
using Xping.Cli.Hosting;
using Xping.Cli.Report.Rendering;
using Xping.Cli.Reporting;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Commands.Auth;

/// <summary>
/// The human text of the auth commands, all of it on stderr (cli-auth-cli-spec §3.1).
/// </summary>
/// <remarks>
/// Stdout carries only a <c>--json</c> document, so the text goes to stderr in every mode, and
/// stderr's own terminal flag decides glyphs and colour.
/// </remarks>
internal sealed class AuthText
{
    private readonly TextWriter _error;

    public AuthText(ConsoleIO io, IEnvironmentVariableProvider environment)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(environment);

        _error = io.Error;
        Capabilities = OutputCapabilities.Resolve(
            forceAscii: false, noColor: false, redirected: !io.IsErrorTerminal, environment.GetVariable);
    }

    /// <summary>
    /// Gets the glyphs and colour this stream supports.
    /// </summary>
    public OutputCapabilities Capabilities { get; }

    /// <summary>
    /// Gets where the Portal lists CLI sessions, with this stream's arrow.
    /// </summary>
    public string SessionsPage => $"Settings {Capabilities.Glyphs.Arrow} Security {Capabilities.Glyphs.Arrow} CLI sessions";

    public void Line(string text = "") => _error.WriteLine(text);

    public void Done(string text) => _error.WriteLine($"{Capabilities.Glyphs.Pass} {text}");

    public void Warning(string text) => _error.WriteLine($"{Capabilities.Glyphs.Warning} {text}");

    public string Emphasis(string text) => Capabilities.Emphasis(text);

    /// <summary>
    /// The block printed after a sign-in (§3.2).
    /// </summary>
    public void SignedIn(CredentialRecord record, ICredentialStore store)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(store);

        Done($"Signed in as {record.Email ?? record.Sub ?? "an unknown user"}");

        if (Workspace(record) is { } workspace)
            Line($"  Workspace  {workspace}");

        if (ShortSession(record.Sid) is { } session)
            Line($"  Session    {session}   (shown on {SessionsPage})");

        Line($"  Stored in  {store.DisplayName}");
    }

    /// <summary>
    /// The workspace line's value: its name with the ID in parentheses, or the ID alone when the
    /// sign-in predates the <c>workspace_name</c> claim.
    /// </summary>
    /// <remarks>
    /// The ID stays because <c>login --workspace</c> takes it. The name is the server's text, so it
    /// is made printable before it reaches the terminal.
    /// </remarks>
    public string? Workspace(CredentialRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.WorkspaceId is not { } id)
            return null;

        return record.WorkspaceName is { } name
            ? $"{TerminalText.Printable(name, Capabilities.Glyphs.Charset)} ({id})"
            : id;
    }

    /// <summary>
    /// The end of a session id, as the Portal's sessions page lets the user match it.
    /// </summary>
    public static string? ShortSession(string? sid) =>
        sid is null ? null : sid.Length <= 6 ? sid : "..." + sid[^6..];
}
