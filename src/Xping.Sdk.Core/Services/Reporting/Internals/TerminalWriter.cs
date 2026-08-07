/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Runtime.InteropServices;
using System.Text;

namespace Xping.Sdk.Core.Services.Reporting.Internals;

/// <summary>
/// Writes developer-facing output to the controlling terminal, falling back to standard output.
/// </summary>
/// <remarks>
/// <para>
/// Bypassing <see cref="Console.SetOut"/> is not sufficient to get output in front of a developer
/// running <c>dotnet test</c>. That only reaches the test host's stdout pipe; the vstest runner then
/// decides whether to relay the pipe based on console logger verbosity, and the default
/// (<c>minimal</c>) discards it. Measured on .NET 10: output written to the test host's stdout is
/// visible only at <c>normal</c> and <c>detailed</c>.
/// </para>
/// <para>
/// Writing to the controlling terminal — <c>/dev/tty</c> on Unix, <c>CONOUT$</c> on Windows — reaches
/// the developer regardless of verbosity, because it skips the runner entirely.
/// </para>
/// <para>
/// When no terminal is attached (CI, IDE test runners, redirected output) this falls back to raw
/// stdout, so the report still lands in captured logs for anyone who raises verbosity.
/// </para>
/// </remarks>
internal static class TerminalWriter
{
    private static readonly Lazy<string?> _terminalPath = new(ResolveTerminalPath);

    /// <summary>
    /// Gets a value indicating whether a controlling terminal is available.
    /// </summary>
    public static bool HasTerminal => _terminalPath.Value != null;

    /// <summary>
    /// Writes text to the terminal, or to standard output when no terminal is attached.
    /// </summary>
    /// <param name="text">The text to write.</param>
    public static void Write(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (_terminalPath.Value is { } path && TryWriteToTerminal(path, text))
            return;

        RawConsoleReportSink.Write(text);
    }

    private static string? ResolveTerminalPath()
    {
        try
        {
            string candidate = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "CONOUT$"
                : "/dev/tty";

            // Probe by opening: existence checks are unreliable for device files, and a terminal
            // that cannot be opened is no more useful than one that does not exist.
            using var probe = new FileStream(candidate, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            return candidate;
        }
        catch (Exception ex) when (IsTerminalUnavailable(ex))
        {
            return null;
        }
    }

    private static bool TryWriteToTerminal(string path, string text)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true
            };

            writer.Write(text);
            return true;
        }
        catch (Exception ex) when (IsTerminalUnavailable(ex))
        {
            return false;
        }
    }

    private static bool IsTerminalUnavailable(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or PlatformNotSupportedException
            or System.Security.SecurityException;
}
