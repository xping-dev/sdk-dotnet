/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Commands.Auth;
using Xping.Cli.Hosting;
using Xping.Cli.Tests.Auth.Store;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Tests.Commands.Auth;

public sealed class AuthTextTests
{
    private readonly AuthText _text = new(
        new ConsoleIO(TextWriter.Null, TextWriter.Null, TextReader.Null, Terminals.All(false)), new NoVariables());

    [Fact]
    public void TheWorkspaceIsItsNameWithTheIdInParentheses() =>
        Assert.Equal("Acme (01J8WORKSPACE)", _text.Workspace(CredentialTestData.Record()));

    [Fact]
    public void WithoutANameTheWorkspaceIsItsId() =>
        Assert.Equal("01J8WORKSPACE", _text.Workspace(CredentialTestData.Record() with { WorkspaceName = null }));

    [Fact]
    public void WithoutAnIdThereIsNoWorkspaceEvenWithAName() =>
        Assert.Null(_text.Workspace(CredentialTestData.Record() with { WorkspaceId = null }));

    [Fact]
    public void TheWorkspaceNameIsMadePrintable() =>
        Assert.DoesNotContain("\u001b", _text.Workspace(CredentialTestData.Record() with { WorkspaceName = "Ac\u001b[2Jme" }), StringComparison.Ordinal);

    [Fact]
    public void AMissingRecordIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => _text.Workspace(null!));

    private sealed class NoVariables : IEnvironmentVariableProvider
    {
        public string? GetVariable(string name) => null;
    }
}
