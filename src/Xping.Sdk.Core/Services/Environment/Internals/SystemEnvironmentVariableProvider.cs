/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Services.Environment.Internals;

/// <summary>
/// Default implementation of <see cref="IEnvironmentVariableProvider"/> that reads from
/// the current process environment via <see cref="System.Environment.GetEnvironmentVariable(string)"/>.
/// </summary>
internal sealed class SystemEnvironmentVariableProvider : IEnvironmentVariableProvider
{
    /// <inheritdoc/>
    public string? GetVariable(string name)
    {
        try
        {
            return System.Environment.GetEnvironmentVariable(name);
        }
        catch
        {
            return null;
        }
    }
}
