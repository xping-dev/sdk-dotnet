/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Runtime.InteropServices;
using Xping.Cli.Auth.Store;

namespace Xping.Cli.Tests.Auth.Store;

/// <summary>
/// A fact that runs on Windows only and is skipped, not failed, elsewhere (cli-auth-cli-spec §18.1).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Windows Credential Manager tests run on Windows only.";
    }
}

/// <summary>
/// A fact that runs on macOS only and is skipped, not failed, elsewhere.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MacOsFactAttribute : FactAttribute
{
    public MacOsFactAttribute()
    {
        if (!OperatingSystem.IsMacOS())
            Skip = "macOS Keychain tests run on macOS only.";
    }
}

/// <summary>
/// A fact that runs on Linux where libsecret is installed, and is skipped elsewhere.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LibSecretFactAttribute : FactAttribute
{
    public LibSecretFactAttribute()
    {
        if (!OperatingSystem.IsLinux())
            Skip = "libsecret tests run on Linux only.";
        else if (!NativeLibrary.TryLoad(LibSecretStore.LibraryName, out _))
            Skip = $"{LibSecretStore.LibraryName} is not installed.";
    }
}
