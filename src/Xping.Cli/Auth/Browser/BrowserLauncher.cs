/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Xping.Cli.Auth.Browser;

/// <summary>
/// Opens a URL in the user's browser (cli-auth-cli-spec §5.1).
/// </summary>
internal interface IBrowserLauncher
{
    /// <summary>
    /// Tries to open <paramref name="url"/>. Never throws, and gives up after about two seconds.
    /// </summary>
    /// <returns>
    /// Whether a browser was asked to open it. The link is printed either way, so a
    /// <see langword="false"/> only changes nothing.
    /// </returns>
    bool TryOpen(Uri url, BrowserEnvironment environment);
}

/// <summary>
/// Starts the processes <see cref="BrowserLauncher"/> needs; the seam its tests replace.
/// </summary>
internal interface ILauncherProcess
{
    /// <summary>
    /// Returns whether an executable called <paramref name="name"/> is on <c>PATH</c>.
    /// </summary>
    bool IsOnPath(string name);

    /// <summary>
    /// Runs <paramref name="fileName"/> with <paramref name="arguments"/>, each passed as one argument
    /// and never through a shell.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when it could not start or exited non-zero within
    /// <paramref name="wait"/>; <see langword="true"/> otherwise, including when it is still running.
    /// </returns>
    bool TryRun(string fileName, IReadOnlyList<string> arguments, TimeSpan wait);

    /// <summary>
    /// Hands <paramref name="url"/> to the Windows shell, which opens the default browser.
    /// </summary>
    bool TryShellOpen(string url);
}

/// <summary>
/// Picks the launcher command per operating system.
/// </summary>
/// <remarks>
/// The URL is always one argument of its own. It holds <c>&amp;</c>, which a shell command line would
/// split on.
/// </remarks>
internal sealed class BrowserLauncher(ILauncherProcess process, HostOs os, ILogger<BrowserLauncher> logger) : IBrowserLauncher
{
    /// <summary>
    /// How long a launcher may take to report failure.
    /// </summary>
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The Windows launcher reached from WSL when <c>wslview</c> is not installed.
    /// </summary>
    public const string WslRundll32 = "/mnt/c/Windows/System32/rundll32.exe";

    public bool TryOpen(Uri url, BrowserEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsHeadless)
            return false;

        string link = url.AbsoluteUri;

        try
        {
            bool opened = environment.Kind == BrowserEnvironmentKind.Wsl
                ? OpenFromWsl(link)
                : os switch
                {
                    HostOs.Windows => process.TryShellOpen(link),
                    HostOs.MacOS => process.TryRun("open", [link], Wait),
                    HostOs.Linux => OpenOnLinux(link),
                    _ => false
                };

            if (opened)
                logger.LogInformation("Opened the browser");
            else
                logger.LogInformation("Could not open a browser");

            return opened;
        }
        catch (Exception ex)
        {
            // Opening the browser is a convenience; the printed link is the contract.
            logger.LogInformation("Could not open a browser: {Reason}", ex.GetType().Name);
            return false;
        }
    }

    private bool OpenFromWsl(string link) =>
        process.IsOnPath("wslview")
            ? process.TryRun("wslview", [link], Wait)
            : process.TryRun(WslRundll32, ["url.dll,FileProtocolHandler", link], Wait);

    private bool OpenOnLinux(string link)
    {
        if (process.IsOnPath("xdg-open"))
            return process.TryRun("xdg-open", [link], Wait);

        if (process.IsOnPath("gio"))
            return process.TryRun("gio", ["open", link], Wait);

        if (process.IsOnPath("sensible-browser"))
            return process.TryRun("sensible-browser", [link], Wait);

        return false;
    }
}

/// <summary>
/// Starts real processes.
/// </summary>
internal sealed class LauncherProcess : ILauncherProcess
{
    public bool IsOnPath(string name)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
            return false;

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(directory, name)))
                    return true;
            }
            catch (ArgumentException)
            {
                // A PATH entry with characters no path can hold; skip it.
            }
        }

        return false;
    }

    public bool TryRun(string fileName, IReadOnlyList<string> arguments, TimeSpan wait)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        try
        {
            using Process? started = Process.Start(startInfo);
            if (started is null)
                return false;

            // Read and dropped, so a chatty launcher cannot fill a pipe and block, and nothing it
            // prints reaches the user's terminal.
            started.OutputDataReceived += static (_, _) => { };
            started.ErrorDataReceived += static (_, _) => { };
            started.BeginOutputReadLine();
            started.BeginErrorReadLine();
            started.StandardInput.Close();

            // Still running after the wait counts as opened: xdg-open can stay alive as long as the
            // browser it started.
            return !started.WaitForExit(wait) || started.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }

    public bool TryShellOpen(string url)
    {
        try
        {
            using Process? started = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
