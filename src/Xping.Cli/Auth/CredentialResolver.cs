/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics;
using Xping.Cli.Auth.Store;
using Xping.Cli.Configuration;

namespace Xping.Cli.Auth;

/// <summary>
/// Where the credential a command uses came from (cli-auth-cli-spec §8.1).
/// </summary>
internal enum CredentialSource
{
    /// <summary>No credential: the command stays local and makes no Cloud request.</summary>
    None,

    /// <summary><c>--api-key</c> on the command line.</summary>
    ApiKeyFlag,

    /// <summary>A sign-in made with <c>xping login</c>.</summary>
    StoredLogin,

    /// <summary><c>XPING_APIKEY</c> or <c>Xping__ApiKey</c>.</summary>
    ApiKeyEnv,

    /// <summary><c>Xping:ApiKey</c> in <c>appsettings*.json</c>.</summary>
    ApiKeyConfig
}

/// <summary>
/// The one credential a command uses, and what else is configured behind it.
/// </summary>
/// <param name="Source">Where the credential came from.</param>
/// <param name="ApiKey">The key, when <paramref name="Source"/> is an API key source.</param>
/// <param name="Login">The sign-in, when <paramref name="Source"/> is <see cref="CredentialSource.StoredLogin"/>.</param>
/// <param name="FallbackApiKey">
/// The ambient key that takes over if the sign-in turns out to be invalid; only set with a sign-in.
/// </param>
/// <param name="ShadowedLogin">
/// Whether a sign-in exists but is not used because <c>--api-key</c> was given.
/// </param>
/// <param name="Warnings">Why stored entries could not be used, worded for the user.</param>
/// <param name="StoreFailures">
/// Why credential stores could not be read at all; <c>auth status</c> exits
/// <see cref="AuthExitCodes.CredentialStoreError"/> on them when nothing else is available (§8.1).
/// </param>
internal sealed record ResolvedCredential(
    CredentialSource Source,
    ConfiguredValue? ApiKey,
    StoredLogin? Login,
    ConfiguredValue? FallbackApiKey,
    bool ShadowedLogin,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> StoreFailures)
{
    /// <summary>
    /// Returns the credential to use once this sign-in has been found invalid: the ambient key, or
    /// none.
    /// </summary>
    /// <remarks>
    /// The caller decides when: only a definitive authentication outcome (<c>invalid_grant</c>, a
    /// second 401) moves on, never a network error, a timeout or a 5xx (§8.1).
    /// </remarks>
    /// <exception cref="InvalidOperationException">This credential is not a sign-in.</exception>
    public ResolvedCredential FallbackToApiKey()
    {
        if (Source != CredentialSource.StoredLogin)
            throw new InvalidOperationException("Only a stored sign-in falls back to an API key.");

        return FallbackApiKey is null
            ? new ResolvedCredential(CredentialSource.None, null, null, null, ShadowedLogin: false, Warnings, StoreFailures)
            : CredentialResolver.FromApiKey(FallbackApiKey, shadowedLogin: false, Warnings, StoreFailures);
    }

    // The key itself must not reach a log line through the generated ToString.
    public override string ToString() =>
        $"ResolvedCredential {{ Source = {Source}, ApiKey = {ApiKey?.Origin}, Login = {Login?.Store.DisplayName}, " +
        $"FallbackApiKey = {FallbackApiKey?.Origin}, ShadowedLogin = {ShadowedLogin} }}";
}

/// <summary>
/// Picks the credential a command uses, without a network call (cli-auth-cli-spec §8.1).
/// </summary>
/// <remarks>
/// Order: <c>--api-key</c>, then the stored sign-in for the Cloud URL, then <c>XPING_APIKEY</c> or
/// <c>Xping:ApiKey</c>, then none. The flag wins because it was typed for this command; the ambient
/// key comes last because on a developer machine it is almost always the SDK's upload key, which
/// cannot read Cloud data (A-8).
/// </remarks>
internal sealed class CredentialResolver(CredentialStoreSelector selector)
{
    /// <summary>
    /// Resolves the credential for <paramref name="configuration"/>'s Cloud URL.
    /// </summary>
    /// <remarks>
    /// A store that cannot be read counts as no sign-in, with its reason in
    /// <see cref="ResolvedCredential.StoreFailures"/>: a broken keychain must not stop a command
    /// that has an API key, or one that only wanted to report locally.
    /// </remarks>
    public async Task<ResolvedCredential> ResolveAsync(CliConfiguration configuration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        ConfiguredValue? apiKey = configuration.ApiKey;
        Redaction.AddSecret(apiKey?.Value);

        string cloudUrl = configuration.CloudUrl.Value;
        StoredLoginLookup lookup = await selector.Select(cloudUrl)
            .ReadAsync(cloudUrl, cancellationToken)
            .ConfigureAwait(false);

        if (apiKey is { Source: ConfigurationSource.Flag })
            return FromApiKey(apiKey, shadowedLogin: lookup.Login is not null, lookup.Warnings, lookup.Failures);

        if (lookup.Login is { } login)
            return new ResolvedCredential(CredentialSource.StoredLogin, null, login, apiKey, ShadowedLogin: false, lookup.Warnings, lookup.Failures);

        return apiKey is null
            ? new ResolvedCredential(CredentialSource.None, null, null, null, ShadowedLogin: false, lookup.Warnings, lookup.Failures)
            : FromApiKey(apiKey, shadowedLogin: false, lookup.Warnings, lookup.Failures);
    }

    internal static ResolvedCredential FromApiKey(
        ConfiguredValue apiKey, bool shadowedLogin, IReadOnlyList<string> warnings, IReadOnlyList<string> storeFailures)
    {
        CredentialSource source = apiKey.Source switch
        {
            ConfigurationSource.Flag => CredentialSource.ApiKeyFlag,
            ConfigurationSource.Environment => CredentialSource.ApiKeyEnv,
            ConfigurationSource.AppSettings => CredentialSource.ApiKeyConfig,
            _ => throw new UnreachableException($"An API key has no {apiKey.Source} source."),
        };

        return new ResolvedCredential(source, apiKey, null, null, shadowedLogin, warnings, storeFailures);
    }
}
