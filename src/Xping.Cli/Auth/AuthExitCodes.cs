/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth;

/// <summary>
/// Exit codes of <c>login</c>, <c>logout</c> and <c>auth status</c>.
/// </summary>
/// <remarks>
/// They start at 10 so they never collide with a <c>report</c> meaning (0-3) and are easy to tell
/// apart in a script. <c>report</c> never returns one of them: Cloud problems there are hints, not
/// failures.
/// </remarks>
internal static class AuthExitCodes
{
    /// <summary>The command did what was asked.</summary>
    public const int Success = 0;

    /// <summary><c>auth status</c> found no credential of any kind.</summary>
    public const int AuthRequired = 10;

    /// <summary>The user denied consent in the browser.</summary>
    public const int LoginDeclined = 11;

    /// <summary>The loopback wait or the device code ran out.</summary>
    public const int LoginTimedOut = 12;

    /// <summary><c>login</c> ran without a terminal, or with <c>CI</c> set.</summary>
    public const int InteractiveRequired = 13;

    /// <summary>Any other sign-in failure: a port could not be bound, a code was rejected.</summary>
    public const int LoginFailed = 14;

    /// <summary>Discovery, the network or TLS failed, or Cloud did not recognise the CLI.</summary>
    public const int CloudUnreachable = 15;

    /// <summary>The server's contract version or minimum CLI version rules this CLI out.</summary>
    public const int CloudVersionMismatch = 16;

    /// <summary>Credentials could not be stored, read or removed.</summary>
    public const int CredentialStoreError = 17;

    /// <summary>The user pressed Ctrl+C. 128 + SIGINT, the code shells use.</summary>
    public const int Cancelled = 130;
}
