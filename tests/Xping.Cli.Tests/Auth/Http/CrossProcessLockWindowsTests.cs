/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Http;
using Xping.Cli.Tests.Auth.Store;

namespace Xping.Cli.Tests.Auth.Http;

/// <summary>
/// The lock file's access rules and share mode on Windows (cli-auth-cli-spec §9.4, §15.3).
/// </summary>
/// <remarks>
/// Pull request CI runs on Linux only, so these run in the credential store workflow with the
/// keychain tests (§17.2).
/// </remarks>
[SupportedOSPlatform("windows")]
[Trait("Category", KeychainStoreScenarios.Category)]
public sealed class CrossProcessLockWindowsTests : IDisposable
{
    private const string CloudUrl = "https://tests.invalid";

    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "xping-cli-lock-windows-tests", Guid.NewGuid().ToString("N"));
    private readonly XpingHome _home;
    private readonly CrossProcessLock _lock;

    public CrossProcessLockWindowsTests()
    {
        _home = new XpingHome(Path.Combine(_scratch, ".xping"));
        _lock = new CrossProcessLock(_home, TimeProvider.System, NullLogger<CrossProcessLock>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_scratch))
            Directory.Delete(_scratch, recursive: true);
    }

    [WindowsFact]
    public async Task TheLockFileIsAccessibleToTheCurrentUserOnly()
    {
        using (IDisposable held = await _lock.AcquireAsync(CloudUrl, CancellationToken.None).ConfigureAwait(false))
            Assert.IsType<FileStream>(held);

        FileSecurity security = new FileInfo(_home.LockFile(CloudUrl)).GetAccessControl();

        Assert.True(security.AreAccessRulesProtected);
        FileSystemAccessRule rule = Assert.Single(
            security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());
        Assert.Equal(WindowsIdentity.GetCurrent().User, rule.IdentityReference);
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.Equal(FileSystemRights.FullControl, rule.FileSystemRights);
    }

    [WindowsFact]
    public async Task ASecondOpenIsRefusedWhileTheLockIsHeldAndSucceedsAfter()
    {
        string path = _home.LockFile(CloudUrl);

        using (IDisposable held = await _lock.AcquireAsync(CloudUrl, CancellationToken.None).ConfigureAwait(false))
        {
            Assert.IsType<FileStream>(held);
            Assert.Throws<IOException>(() => PrivateFiles.OpenExclusive(path));
        }

        // The file exists now; opening it again sets its rules again rather than failing.
        using IDisposable again = await _lock.AcquireAsync(CloudUrl, CancellationToken.None).ConfigureAwait(false);
        Assert.IsType<FileStream>(again);
    }
}
