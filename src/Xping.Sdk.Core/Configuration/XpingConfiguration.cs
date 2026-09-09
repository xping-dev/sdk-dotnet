/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.ComponentModel.DataAnnotations;

namespace Xping.Sdk.Core.Configuration;

/// <summary>
/// Configuration options for the Xping SDK.
/// </summary>
public sealed class XpingConfiguration
{
    /// <summary>
    /// The environment name recorded when <see cref="Environment"/> is left unset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately not a deployment name. The SDK cannot know which deployed environment a suite
    /// targeted, so naming one would be a guess: an unconfigured pipeline running smoke tests
    /// against production would be stamped with whatever constant was chosen here. "Default"
    /// describes the configuration state - nothing was set - which is the one thing that is true.
    /// </para>
    /// <para>
    /// It is a full environment for scoring purposes, never a value to be filtered out. For most
    /// projects it is the only environment there is, so excluding it would empty every
    /// per-environment comparison at once.
    /// </para>
    /// </remarks>
    public const string DefaultEnvironment = "Default";

    /// <summary>
    /// Gets or sets the Xping API endpoint URL.
    /// </summary>
    [Required(ErrorMessage = "ApiEndpoint is required")]
    [Url(ErrorMessage = "ApiEndpoint must be a valid HTTP or HTTPS URL")]
    public string ApiEndpoint { get; set; } = "https://upload.xping.io/v1";

    /// <summary>
    /// Gets or sets the API key for authentication.
    /// Required in <see cref="XpingMode.Cloud"/> mode; ignored in <see cref="XpingMode.LocalOnly"/> mode.
    /// </summary>
    /// <remarks>
    /// This property intentionally carries no <c>[Required]</c> data annotation. Requiring it
    /// unconditionally would make <c>ValidateDataAnnotations()</c> throw before
    /// <see cref="ResolveMode"/> ever runs, making local-only operation impossible.
    /// Presence is enforced by <see cref="Validate()"/> only when the resolved mode needs it.
    /// </remarks>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the project every execution in this session is pinned to. Optional.
    /// Ignored in <see cref="XpingMode.LocalOnly"/> mode.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When left unset, Xping derives the project from the test assembly each execution belongs to,
    /// so a solution-wide <c>dotnet test</c> reports one project per test project. That is the
    /// default because a test assembly is already the unit a developer thinks in, and it needs no
    /// bookkeeping to keep unique.
    /// </para>
    /// <para>
    /// Set this only to override that — for example in a monorepo where several test assemblies
    /// should report into a single project. It is a hard pin: every execution in the session lands
    /// in this project regardless of which assembly it came from.
    /// </para>
    /// </remarks>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the batch size for uploading test executions.
    /// </summary>
    [Range(1, 1000, ErrorMessage = "BatchSize must be between 1 and 1000")]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the flush interval for automatically uploading batches.
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the deployed environment the tests targeted (e.g. "Staging", "Production").
    /// Optional; when left unset the SDK records <see cref="DefaultEnvironment"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This names <i>what the suite tested against</i>, not <i>where the suite ran</i>. A developer's
    /// laptop run and the CI run of the same suite belong in the same environment, because they are
    /// the same environment; they differ in how far the revision under test can be trusted, which is
    /// recorded separately. Scores are computed per (test, environment), so every extra name splits a
    /// test's history - one environment per pull request fragments it permanently.
    /// </para>
    /// <para>
    /// Nothing is inferred from the host. <c>ASPNETCORE_ENVIRONMENT</c> and <c>DOTNET_ENVIRONMENT</c>
    /// are deliberately not read: they state how the app under test should configure itself, which
    /// overlaps with but is not the same question, and a base image or shell profile setting one
    /// inconsistently would manufacture a split that looks legitimate. Teams that want that value can
    /// pass it through explicitly with <c>XPING_ENVIRONMENT: ${{ env.ASPNETCORE_ENVIRONMENT }}</c>.
    /// </para>
    /// </remarks>
    public string? Environment { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the SDK is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the operating mode. Defaults to <see cref="XpingMode.Auto"/>, which resolves to
    /// <see cref="XpingMode.Cloud"/> when an <see cref="ApiKey"/> is present and
    /// <see cref="XpingMode.LocalOnly"/> otherwise. Use <see cref="ResolveMode"/> to obtain the
    /// effective mode.
    /// </summary>
    public XpingMode Mode { get; set; } = XpingMode.Auto;

    /// <summary>
    /// Gets or sets a value indicating whether to capture stack traces for failed tests.
    /// </summary>
    public bool CaptureStackTraces { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable compression for uploads.
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed uploads.
    /// </summary>
    [Range(0, 10, ErrorMessage = "MaxRetries must be between 0 and 10")]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the timeout for upload operations.
    /// </summary>
    public TimeSpan UploadTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether to detect pull request context from CI/CD environment variables
    /// and include it in session uploads to enable PR comment posting.
    /// </summary>
    public bool EnablePullRequestDetection { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include the local git author name
    /// (read from <c>.git/config [user] name</c>) in environment metadata when running
    /// outside a CI environment.
    /// Disabled by default to prevent unintentional collection of developer PII.
    /// </summary>
    public bool CollectLocalGitAuthor { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether strict mode is enabled.
    /// When <see langword="true"/>, configuration errors cause the SDK to throw a
    /// <see cref="Xping.Sdk.Core.Exceptions.XpingConfigurationException"/> during initialization, allowing callers to
    /// fail fast and surface configuration problems explicitly.
    /// When <see langword="false"/> (default), configuration errors are logged and the SDK is
    /// silently disabled where supported.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the core SDK, strict mode surfaces configuration problems by throwing
    /// <see cref="Xping.Sdk.Core.Exceptions.XpingConfigurationException"/> so that hosts and orchestrators can control
    /// how failures are handled (for example, by aborting the run or marking it as failed).
    /// Some test framework adapters (e.g. NUnit, xUnit, MSTest integrations) may choose to
    /// translate these configuration failures into a call to
    /// <see cref="Environment.FailFast(string, Exception)"/> to immediately terminate the
    /// process and prevent subsequent tests from running untracked, especially in CI/CD
    /// environments.
    /// </para>
    /// <para>
    /// Strict mode can also be enabled via the <c>XPING_STRICTMODE</c> environment variable.
    /// This is recommended for production CI/CD pipelines where observability must be guaranteed.
    /// </para>
    /// </remarks>
    public bool StrictMode { get; set; }

    /// <summary>
    /// Resolves <see cref="Mode"/> to a concrete operating mode.
    /// </summary>
    /// <returns>
    /// The effective <see cref="XpingMode"/>: never <see cref="XpingMode.Auto"/>.
    /// </returns>
    /// <remarks>
    /// <para>Resolution order:</para>
    /// <list type="number">
    /// <item><description><see cref="Enabled"/> is <see langword="false"/> → <see cref="XpingMode.Disabled"/>.</description></item>
    /// <item><description><see cref="Mode"/> was set explicitly → that mode.</description></item>
    /// <item><description><see cref="StrictMode"/> is <see langword="true"/> → <see cref="XpingMode.Cloud"/>.</description></item>
    /// <item><description><see cref="ApiKey"/> present → <see cref="XpingMode.Cloud"/>.</description></item>
    /// <item><description>Otherwise → <see cref="XpingMode.LocalOnly"/>.</description></item>
    /// </list>
    /// <para>
    /// Strict mode deliberately forces <see cref="XpingMode.Cloud"/>. Its purpose is to guarantee
    /// observability in CI, so a missing API key must remain a hard configuration error rather than
    /// silently degrading a pipeline to local-only collection.
    /// </para>
    /// </remarks>
    public XpingMode ResolveMode()
    {
        if (!Enabled)
            return XpingMode.Disabled;

        // Mode is externally bindable, so an out-of-range numeric value can reach this property
        // (Xping:Mode=99, or a cast in code). Treating it as an implicit mode would select a no-op
        // uploader while leaving local-only network suppression off - a state no configuration is
        // supposed to produce. Fall back to Auto resolution; Validate() reports it separately.
        if (Mode != XpingMode.Auto && IsDefinedMode(Mode))
            return Mode;

        if (StrictMode)
            return XpingMode.Cloud;

        return HasApiKey ? XpingMode.Cloud : XpingMode.LocalOnly;
    }

    /// <summary>
    /// Validates the configuration and returns a list of validation errors.
    /// Credential requirements are evaluated against the resolved <see cref="XpingMode"/>.
    /// </summary>
    /// <returns>A list of validation error messages, or an empty list if valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!IsDefinedMode(Mode))
        {
            errors.Add(
                $"Mode has an undefined value ({(int)Mode}). " +
                "Valid values are Auto, LocalOnly, Cloud, and Disabled.");
        }

        XpingMode mode = ResolveMode();

        // The API key is only meaningful when the SDK will actually talk to the platform.
        if (mode == XpingMode.Cloud)
        {
            // ApiKey is the only credential Cloud mode requires. ProjectId is optional: when it is
            // unset the project is derived from each execution's test assembly.
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                errors.Add(StrictMode && Mode == XpingMode.Auto
                    ? "ApiKey is required when StrictMode is enabled."
                    : "ApiKey is required in Cloud mode.");
            }
        }

        if (string.IsNullOrWhiteSpace(ApiEndpoint))
        {
            errors.Add("ApiEndpoint is required.");
        }
        else if (!Uri.TryCreate(ApiEndpoint, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("ApiEndpoint must be a valid HTTP or HTTPS URL.");
        }

        if (BatchSize <= 0)
        {
            errors.Add("BatchSize must be greater than zero.");
        }

        if (BatchSize > 1000)
        {
            errors.Add("BatchSize cannot exceed 1000.");
        }

        if (FlushInterval <= TimeSpan.Zero)
        {
            errors.Add("FlushInterval must be greater than zero.");
        }

        if (MaxRetries < 0)
        {
            errors.Add("MaxRetries cannot be negative.");
        }

        if (MaxRetries > 10)
        {
            errors.Add("MaxRetries cannot exceed 10.");
        }

        if (RetryDelay < TimeSpan.Zero)
        {
            errors.Add("RetryDelay cannot be negative.");
        }

        if (UploadTimeout <= TimeSpan.Zero)
        {
            errors.Add("UploadTimeout must be greater than zero.");
        }

        return errors;
    }

    /// <summary>
    /// Determines whether the configuration is valid.
    /// </summary>
    /// <returns><c>true</c> if the configuration is valid; otherwise, <c>false</c>.</returns>
    public bool IsValid()
    {
        return Validate().Count == 0;
    }

    private static bool IsDefinedMode(XpingMode mode) =>
        mode is XpingMode.Auto or XpingMode.LocalOnly or XpingMode.Cloud or XpingMode.Disabled;

    /// <summary>
    /// Gets the environment to record: the configured one, or <see cref="DefaultEnvironment"/>.
    /// </summary>
    /// <remarks>
    /// Whitespace counts as unset, so a blank placeholder left in a configuration template
    /// (<c>"Environment": ""</c>) means "not configured" rather than "the empty-string environment".
    /// Trimmed for the same reason a name is not fabricated: <c>"Staging "</c> and <c>"Staging"</c>
    /// are one environment, and letting them be two would split the very history this field keys.
    /// </remarks>
    internal string ResolvedEnvironment =>
        string.IsNullOrWhiteSpace(Environment) ? DefaultEnvironment : Environment!.Trim();

    /// <summary>
    /// Gets a value indicating whether an API key has been supplied.
    /// </summary>
    /// <remarks>
    /// This alone decides whether <see cref="XpingMode.Auto"/> resolves to
    /// <see cref="XpingMode.Cloud"/>. <see cref="ProjectId"/> is deliberately not part of it: a
    /// project is named by the test assembly when none is pinned, so requiring one would make the
    /// common case unreachable.
    /// </remarks>
    internal bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// Gets the pinned project, or <see langword="null"/> when project identity is derived from the
    /// test assembly.
    /// </summary>
    /// <remarks>
    /// Whitespace is treated as unset, so a blank placeholder left in a configuration template
    /// (<c>"ProjectId": ""</c>) means "derive" rather than "pin the empty string".
    /// </remarks>
    internal string? ProjectPin => string.IsNullOrWhiteSpace(ProjectId) ? null : ProjectId!.Trim();
}
