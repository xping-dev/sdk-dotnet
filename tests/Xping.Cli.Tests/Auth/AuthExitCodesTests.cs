/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth;

namespace Xping.Cli.Tests.Auth;

public sealed class AuthExitCodesTests
{
    // Scripts and agents branch on these numbers, and docs/cli/command-reference.md repeats them.
    [Theory]
    [InlineData(AuthExitCodes.Success, 0)]
    [InlineData(AuthExitCodes.AuthRequired, 10)]
    [InlineData(AuthExitCodes.LoginDeclined, 11)]
    [InlineData(AuthExitCodes.LoginTimedOut, 12)]
    [InlineData(AuthExitCodes.InteractiveRequired, 13)]
    [InlineData(AuthExitCodes.LoginFailed, 14)]
    [InlineData(AuthExitCodes.CloudUnreachable, 15)]
    [InlineData(AuthExitCodes.CloudVersionMismatch, 16)]
    [InlineData(AuthExitCodes.CredentialStoreError, 17)]
    [InlineData(AuthExitCodes.Cancelled, 130)]
    public void EachCodeKeepsTheNumberTheSpecGivesIt(int actual, int expected)
    {
        Assert.Equal(expected, actual);
    }
}
