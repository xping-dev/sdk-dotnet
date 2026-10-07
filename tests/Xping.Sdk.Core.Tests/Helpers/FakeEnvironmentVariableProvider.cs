/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.Environment;

namespace Xping.Sdk.Core.Tests.Helpers;

/// <summary>
/// An environment that holds only the variables a test sets, so tests never touch the process
/// environment and never see the host's own CI variables.
/// </summary>
internal sealed class FakeEnvironmentVariableProvider : IEnvironmentVariableProvider
{
    private readonly Dictionary<string, string?> _variables = new(StringComparer.Ordinal);

    // A null value leaves the variable unset, which lets a [Theory] pass null for "absent".
    public string? this[string name]
    {
        set => _variables[name] = value;
    }

    public string? GetVariable(string name) =>
        _variables.TryGetValue(name, out string? value) ? value : null;
}
