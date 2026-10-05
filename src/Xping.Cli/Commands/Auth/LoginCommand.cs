/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth;
using Xping.Cli.Hosting;

namespace Xping.Cli.Commands.Auth;

/// <summary>
/// Signs in to Xping Cloud from a browser.
/// </summary>
internal sealed class LoginCommand(ConsoleIO io)
{
    public async Task<int> RunAsync(bool json, CancellationToken cancellationToken)
    {
        _ = json;
        cancellationToken.ThrowIfCancellationRequested();

        await io.Error.WriteLineAsync("xping login is not implemented yet.").ConfigureAwait(false);
        return AuthExitCodes.LoginFailed;
    }
}
