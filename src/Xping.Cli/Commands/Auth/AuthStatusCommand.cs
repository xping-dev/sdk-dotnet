/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth;
using Xping.Cli.Hosting;

namespace Xping.Cli.Commands.Auth;

/// <summary>
/// Reports which Xping Cloud credential the CLI would use, without a network call.
/// </summary>
internal sealed class AuthStatusCommand(ConsoleIO io)
{
    public async Task<int> RunAsync(bool json, CancellationToken cancellationToken)
    {
        _ = json;
        cancellationToken.ThrowIfCancellationRequested();

        await io.Error.WriteLineAsync("xping auth status is not implemented yet.").ConfigureAwait(false);
        return AuthExitCodes.LoginFailed;
    }
}
