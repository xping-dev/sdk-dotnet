/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth;
using Xping.Cli.Hosting;

namespace Xping.Cli.Commands.Auth;

/// <summary>
/// Revokes the stored sign-in and removes it from this machine.
/// </summary>
internal sealed class LogoutCommand(ConsoleIO io)
{
    public async Task<int> RunAsync(bool json, CancellationToken cancellationToken)
    {
        _ = json;
        cancellationToken.ThrowIfCancellationRequested();

        await io.Error.WriteLineAsync("xping logout is not implemented yet.").ConfigureAwait(false);
        return AuthExitCodes.LoginFailed;
    }
}
