/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Http;
using Xping.Cli.Cloud.Projects;
using Xping.Cli.Configuration;
using Xping.Cli.Report.Contract;

namespace Xping.Cli.Cloud;

/// <summary>
/// What a report knows that the enrichment needs beyond the envelope.
/// </summary>
/// <param name="Directory">The directory the report is about; its <c>appsettings*.json</c> are read.</param>
/// <param name="ProjectFlag">The <c>report --project</c> value, if given.</param>
/// <param name="SessionPins">The project pin each assembly's newest session was recorded with.</param>
internal sealed record EnrichmentRequest(
    string Directory,
    string? ProjectFlag,
    IReadOnlyDictionary<string, string> SessionPins);

/// <summary>
/// One line for stderr about why Cloud data is missing.
/// </summary>
/// <param name="Text">The line.</param>
/// <param name="Always">
/// Printed even when stderr is not a terminal and <c>--verbose</c> is off: only the
/// <c>login-required</c> line, because a sign-in is the fix and a human reading a CI or agent log
/// needs to know it (cli-auth-cli-spec §10.4).
/// </param>
internal sealed record CloudHint(string Text, bool Always);

/// <summary>
/// The envelope with whatever Cloud data could be read, and the hints to print.
/// </summary>
internal sealed record EnrichmentResult(ReportEnvelope Envelope, IReadOnlyList<CloudHint> Hints);

/// <summary>
/// Adds Xping Cloud's view of each reported test to a local report (cli-auth-cli-spec §11).
/// </summary>
/// <remarks>
/// <para>
/// Never required and never in charge: every failure ends in the local report exactly as it would
/// have been, plus at most a hint line, and nothing here can change the report's exit code. The
/// whole enrichment runs inside <see cref="Budget"/> so a slow Cloud cannot make a local report
/// slow.
/// </para>
/// <para>
/// The fallback of §8.1 is decided here because only the caller of the pipeline sees the outcome:
/// a sign-in that is definitively over moves on to the ambient API key once; a network error,
/// timeout, 5xx or version mismatch does not, since Cloud down for one credential is Cloud down for
/// all.
/// </para>
/// </remarks>
internal sealed class CloudEnricher(
    CliConfigurationLoader configurationLoader,
    CredentialResolver resolver,
    ICloudApiClientFactory clientFactory,
    ProjectResolver projectResolver,
    TimeProvider timeProvider,
    ILogger<CloudEnricher> logger)
{
    /// <summary>
    /// The wall-clock limit on one report's enrichment (§10.2, §11.5).
    /// </summary>
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The most test reads in flight at once (§11.5).
    /// </summary>
    public const int MaxConcurrency = 4;

    private const string Unavailable = "Cloud data unavailable: ";
    private const string LoginRequiredReason = "your sign-in is no longer valid";

    /// <summary>
    /// Returns <paramref name="envelope"/> with Cloud data added where it could be read.
    /// </summary>
    /// <remarks>
    /// Never throws except for the caller's own cancellation: whatever goes wrong here costs the
    /// Cloud data, never the local report or its exit code.
    /// </remarks>
    public async Task<EnrichmentResult> EnrichAsync(
        ReportEnvelope envelope, EnrichmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await EnrichCoreAsync(envelope, request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(ex, "Cloud enrichment failed: {Message}", ex.Message);
            return new EnrichmentResult(envelope, []);
        }
    }

    private async Task<EnrichmentResult> EnrichCoreAsync(
        ReportEnvelope envelope, EnrichmentRequest request, CancellationToken cancellationToken)
    {
        CliConfiguration configuration;
        try
        {
            configuration = configurationLoader.Load(request.Directory, request.ProjectFlag);
        }
        catch (CliConfigurationException ex)
        {
            return new EnrichmentResult(envelope, [new CloudHint(Unavailable + ex.Message, Always: false)]);
        }

        ResolvedCredential credential = await resolver.ResolveAsync(configuration, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("credential: {Credential}", Describe(credential));

        foreach (string problem in credential.StoreFailures.Concat(credential.Warnings))
            logger.LogInformation("{Problem}", problem);

        if (credential.Source == CredentialSource.None)
        {
            // A sign-in file that was refused or corrupt is the one silent "no credential" the user
            // can act on (§11.6); every other way of having none is the default local mode.
            return new EnrichmentResult(
                envelope,
                credential.Warnings.Count > 0 ? [new CloudHint(Unavailable + credential.Warnings[0], Always: false)] : []);
        }

        string cloudUrl = configuration.CloudUrl.Value;
        IReadOnlyList<Target> targets = Targets(envelope);
        if (targets.Count == 0)
        {
            var notAttempted = new CloudContextDto(cloudUrl, CredentialToken(credential), WorkspaceOf(credential), "not-attempted", null, null);
            return new EnrichmentResult(WithContext(envelope, notAttempted), []);
        }

        using var budget = new CancellationTokenSource(Budget, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(budget.Token, cancellationToken);

        var hints = new List<CloudHint>();
        Attempt attempt = await AttemptAsync(credential, cloudUrl, configuration, request, targets, linked.Token).ConfigureAwait(false);

        if (attempt.LoginRequired && credential.Source == CredentialSource.StoredLogin)
        {
            hints.Add(new CloudHint(Unavailable + LoginRequiredReason + ". Run xping login.", Always: true));

            ResolvedCredential fallback = credential.FallbackToApiKey();
            if (fallback.Source != CredentialSource.None)
            {
                logger.LogInformation(
                    "credential: fell back to API key ({Origin}) because the stored login is no longer valid", fallback.ApiKey?.Origin);

                credential = fallback;
                attempt = await AttemptAsync(credential, cloudUrl, configuration, request, targets, linked.Token).ConfigureAwait(false);
            }
        }

        if (attempt.Hint is { } hint && !attempt.LoginRequired)
            hints.Add(hint);

        cancellationToken.ThrowIfCancellationRequested();

        var context = new CloudContextDto(
            cloudUrl,
            CredentialToken(credential),
            WorkspaceOf(credential),
            attempt.Status(targets.Count),
            attempt.LoginRequired ? LoginRequiredReason : attempt.Reason,
            attempt.ProjectFor(envelope.Context?.Assembly));

        return new EnrichmentResult(Apply(WithContext(envelope, context), attempt.Tests), hints);
    }

    /// <summary>
    /// Reads every target with one credential, until done, refused, or out of budget.
    /// </summary>
    private async Task<Attempt> AttemptAsync(
        ResolvedCredential credential,
        string cloudUrl,
        CliConfiguration configuration,
        EnrichmentRequest request,
        IReadOnlyList<Target> targets,
        CancellationToken cancellationToken)
    {
        var attempt = new Attempt();
        var lookup = new ProjectLookup(cloudUrl, WorkspaceOf(credential), configuration.ProjectId, request.SessionPins);

        try
        {
            using ICloudApiClient client = await clientFactory.CreateAsync(credential, cloudUrl, cancellationToken).ConfigureAwait(false);

            try
            {
                foreach (IGrouping<string, Target> assembly in targets.GroupBy(t => t.Assembly, StringComparer.Ordinal))
                    await ReadAssemblyAsync(client, assembly.Key, [.. assembly], lookup, attempt, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                foreach (string warning in client.Warnings)
                    logger.LogInformation("{Warning}", warning);
            }
        }
        catch (LoginRequiredException)
        {
            attempt.LoginRequired = true;
        }
        catch (AuthFailureException ex) when (ex.ErrorCode == AuthErrorCodes.CloudUnreachable)
        {
            attempt.Fail($"could not reach {ex.Target ?? cloudUrl} ({ex.Reason ?? "no answer"}). Showing local results only.");
        }
        catch (AuthFailureException ex)
        {
            attempt.Fail(ex.Message);
        }
        catch (CloudApiException ex)
        {
            attempt.Fail(ex.Message);
        }
        catch (OperationCanceledException)
        {
            // The token is the budget linked with the caller's. A caller's cancellation is rethrown
            // by EnrichAsync, so whatever reaches this point reads as the budget running out.
            attempt.Fail($"could not reach {cloudUrl} (timeout). Showing local results only.");
        }
        catch (Exception ex)
        {
            // Never break the report: a defect in this code costs the Cloud data, not the local
            // result. The message goes to the scrubbed verbose log and is kept out of the hint.
            logger.LogInformation(ex, "Cloud enrichment failed: {Message}", ex.Message);
            attempt.Fail("an unexpected error occurred. Showing local results only.");
        }

        return attempt;
    }

    private async Task ReadAssemblyAsync(
        ICloudApiClient client,
        string assembly,
        IReadOnlyList<Target> targets,
        ProjectLookup lookup,
        Attempt attempt,
        CancellationToken cancellationToken)
    {
        ProjectBinding? binding = await projectResolver.ResolveAsync(client, assembly, lookup, cancellationToken).ConfigureAwait(false);
        if (binding is null)
        {
            attempt.Unresolved(assembly);
            return;
        }

        attempt.Bind(assembly, binding);
        int found = await FetchAsync(client, binding, targets, attempt, cancellationToken).ConfigureAwait(false);

        // A remembered name match whose project now answers nothing has most likely been renamed or
        // removed; the match is dropped and the list read again, once (§11.2).
        if (found == 0 && await projectResolver.RebindAsync(client, assembly, binding, lookup, cancellationToken).ConfigureAwait(false) is { } fresh)
        {
            attempt.Bind(assembly, fresh);
            await FetchAsync(client, fresh, targets, attempt, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<int> FetchAsync(
        ICloudApiClient client, ProjectBinding binding, IReadOnlyList<Target> targets, Attempt attempt, CancellationToken cancellationToken)
    {
        int found = 0;
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = cancellationToken };

        await Parallel.ForEachAsync(targets, options, async (target, token) =>
        {
            CloudTest? test = await client.GetTestAsync(binding.Key, target.Fingerprint, token).ConfigureAwait(false);
            attempt.Answered[target.Fingerprint] = true;

            if (test is not null)
            {
                attempt.Tests[target.Fingerprint] = ToDto(test);
                Interlocked.Increment(ref found);
            }
        }).ConfigureAwait(false);

        return found;
    }

    private CloudTestDto ToDto(CloudTest test) =>
        new(
            test.ConfidenceScore,
            test.ConfidenceScore is null ? null : Kebab(test.ScoreCategory),
            Kebab(test.EvidenceLevel) ?? test.EvidenceLevel,
            test.TotalExecutions,
            Kebab(test.ScoreTrend),
            test.ScoreDelta,
            test.IsFlaky,
            test.LastExecutedAtUtc,
            timeProvider.GetUtcNow());

    /// <summary>
    /// Every single-test subject in the envelope, once per fingerprint.
    /// </summary>
    /// <remarks>
    /// Group findings have no fingerprint of their own and are left alone: a cluster's Cloud view
    /// would be one of its members' presented as the whole.
    /// </remarks>
    private static IReadOnlyList<Target> Targets(ReportEnvelope envelope)
    {
        IEnumerable<SubjectDto> subjects = envelope.Findings.Select(f => f.Subject)
            .Concat(envelope.LatestRun?.Failures.Select(f => f.Subject) ?? []);

        return
        [
            .. subjects
                .Where(s => s.Fingerprint is { Length: > 0 } && s.Assembly is { Length: > 0 })
                .Select(s => new Target(s.Assembly!, s.Fingerprint!))
                .DistinctBy(t => t.Fingerprint, StringComparer.Ordinal)
        ];
    }

    private static ReportEnvelope WithContext(ReportEnvelope envelope, CloudContextDto cloud) =>
        envelope with
        {
            Context = envelope.Context is { } context
                ? context with { Cloud = cloud }
                : new ContextDto(null, null, null, cloud)
        };

    private static ReportEnvelope Apply(ReportEnvelope envelope, ConcurrentDictionary<string, CloudTestDto> tests)
    {
        if (tests.IsEmpty)
            return envelope;

        CloudTestDto? For(SubjectDto subject) =>
            subject.Fingerprint is { } fingerprint && tests.TryGetValue(fingerprint, out CloudTestDto? test) ? test : null;

        return envelope with
        {
            Findings = [.. envelope.Findings.Select(f => f with { Cloud = For(f.Subject) })],
            LatestRun = envelope.LatestRun is { } latest
                ? latest with { Failures = [.. latest.Failures.Select(f => f with { Cloud = For(f.Subject) })] }
                : null
        };
    }

    private static string CredentialToken(ResolvedCredential credential) =>
        credential.Source == CredentialSource.StoredLogin ? "stored-login" : "api-key";

    private static string? WorkspaceOf(ResolvedCredential credential) => credential.Login?.Record.WorkspaceId;

    /// <summary>
    /// The <c>--verbose</c> line naming the credential in use (§8.2). Never the key or a token.
    /// </summary>
    private string Describe(ResolvedCredential credential)
    {
        if (credential.Login is { } login)
        {
            var text = new StringBuilder($"stored login ({login.Store.DisplayName})");

            if (login.Record.WorkspaceId is { } workspace)
                text.Append(CultureInfo.InvariantCulture, $", workspace {workspace}");

            if (login.Record.AccessTokenExpiresAt is { } expiresAt)
            {
                TimeSpan left = expiresAt - timeProvider.GetUtcNow();
                text.Append(left > TimeSpan.Zero
                    ? string.Create(CultureInfo.InvariantCulture, $", expires in {Math.Ceiling(left.TotalMinutes)} min")
                    : ", access token expired");
            }

            return text.ToString();
        }

        return credential.ApiKey is { } key ? $"API key ({key.Origin})" : "none (local only)";
    }

    /// <summary>
    /// <c>ModeratelyReliable</c> → <c>moderately-reliable</c>.
    /// </summary>
    internal static string? Kebab(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = new StringBuilder(value.Length + 4);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c is '_' or ' ' or '-')
            {
                if (text.Length > 0 && text[^1] != '-')
                    text.Append('-');

                continue;
            }

            if (char.IsUpper(c) && i > 0 && text.Length > 0 && text[^1] != '-')
                text.Append('-');

            text.Append(char.ToLowerInvariant(c));
        }

        return text.ToString();
    }

    private sealed record Target(string Assembly, string Fingerprint);

    /// <summary>
    /// What one credential's pass produced.
    /// </summary>
    private sealed class Attempt
    {
        /// <summary>Gets the tests Cloud had data for, by fingerprint.</summary>
        public ConcurrentDictionary<string, CloudTestDto> Tests { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the fingerprints Cloud answered for, with data or a 404.</summary>
        public ConcurrentDictionary<string, bool> Answered { get; } = new(StringComparer.Ordinal);

        public bool LoginRequired { get; set; }

        public CloudHint? Hint { get; private set; }

        /// <summary>Gets the hint without its lead-in, for <c>context.cloud.reason</c>.</summary>
        public string? Reason { get; private set; }

        private ConcurrentDictionary<string, CloudProjectDto> Projects { get; } = new(StringComparer.Ordinal);

        public void Bind(string assembly, ProjectBinding binding) =>
            Projects[assembly] = new CloudProjectDto(assembly, binding.Key, binding.Source);

        /// <summary>
        /// The binding of the report's own assembly; failing that, of any assembly read.
        /// </summary>
        public CloudProjectDto? ProjectFor(string? assembly) =>
            assembly is not null && Projects.TryGetValue(assembly, out CloudProjectDto? project)
                ? project
                : Projects.Values.OrderBy(p => p.Assembly, StringComparer.Ordinal).FirstOrDefault();

        public void Unresolved(string assembly)
        {
            if (Hint is not null)
                return;

            Hint = new CloudHint(
                $"Cloud data unavailable for {assembly}: no matching Cloud project. Use --project <key>.", Always: false);
            Reason = $"no matching Cloud project for {assembly}. Use --project <key>.";
        }

        public void Fail(string reason)
        {
            Hint = new CloudHint(Unavailable + reason, Always: false);
            Reason = reason;
        }

        public string Status(int targets)
        {
            if (LoginRequired)
                return "login-required";

            if (Hint is null && Answered.Count >= targets)
                return "ok";

            return Tests.IsEmpty ? "unavailable" : "partial";
        }
    }
}
