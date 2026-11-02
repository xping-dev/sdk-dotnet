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
using Xping.Cli.Cloud.Digest;
using Xping.Cli.Cloud.Projects;
using Xping.Cli.Configuration;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Sdk.Core.Models;

namespace Xping.Cli.Cloud;

/// <summary>
/// What a report knows that the enrichment needs beyond the envelope.
/// </summary>
/// <param name="Directory">The directory the report is about; its <c>appsettings*.json</c> are read.</param>
/// <param name="ProjectFlag">The <c>report --project</c> value, if given.</param>
/// <param name="SessionPins">The project pin each assembly's newest session was recorded with.</param>
/// <param name="Sessions">The window's sessions, newest first: the signatures and session keys sent.</param>
/// <param name="Findings">Every finding, ranked, before <c>--top</c> cut the envelope's list.</param>
internal sealed record EnrichmentRequest(
    string Directory,
    string? ProjectFlag,
    IReadOnlyDictionary<string, string> SessionPins,
    IReadOnlyList<TestSession> Sessions,
    IReadOnlyList<Finding> Findings);

/// <summary>
/// One line for stderr about why Cloud data is missing.
/// </summary>
/// <param name="Text">The line.</param>
/// <param name="Always">
/// Printed even when stderr is not a terminal and <c>--verbose</c> is off: only for
/// <c>login-required</c> and <c>forbidden</c>, whose fix only a human can apply, and a human reading
/// a CI or agent log needs to know it (contract §8.2 F2).
/// </param>
internal sealed record CloudHint(string Text, bool Always);

/// <summary>
/// The envelope with whatever Cloud data could be read, and the hints to print.
/// </summary>
internal sealed record EnrichmentResult(ReportEnvelope Envelope, IReadOnlyList<CloudHint> Hints);

/// <summary>
/// How the Cloud layer went, decided once at the end of an enrichment (cloud-data-in-cli-cli-spec §3.4).
/// </summary>
/// <param name="Status">The <c>context.cloud.status</c>.</param>
/// <param name="Reason">The <c>context.cloud.reason</c>.</param>
/// <param name="Hint">The one stderr line, or null.</param>
internal sealed record CloudOutcome(string Status, string Reason, CloudHint? Hint);

/// <summary>
/// Adds Xping Cloud's view of each reported test to a local report (cloud-data-in-cli-cli-spec §3, §4).
/// </summary>
/// <remarks>
/// <para>
/// Never required and never in charge: every failure ends in the local report exactly as it would
/// have been, plus <c>context.cloud</c> and at most one hint line, and nothing here can change the
/// report's exit code. The whole enrichment runs inside a <see cref="CloudBudget"/> so a slow Cloud
/// cannot make a local report slow.
/// </para>
/// <para>
/// The fallback of contract §9.6 is decided here because only the caller of the pipeline sees the
/// outcome: a sign-in that is definitively over moves on to the ambient API key once, for the tests
/// still unanswered; a network error, timeout, 5xx or version mismatch does not, since Cloud down
/// for one credential is Cloud down for all.
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
    /// The most digest requests in flight at once (contract §7.2).
    /// </summary>
    public const int MaxConcurrency = 4;

    /// <summary>
    /// The most tests in one digest request (contract §6.1).
    /// </summary>
    public const int MaxDigestBatchSize = 100;

    /// <summary>
    /// The most session keys in one digest request (contract §6.1).
    /// </summary>
    public const int MaxSessionKeysPerRequest = 5;

    /// <summary>
    /// The runs <c>report --test</c> reads from the Cloud history (contract §6.3).
    /// </summary>
    public const int RunsLimit = 50;

    /// <summary>
    /// The signature algorithm this CLI's hashes are computed with (contract §5.2).
    /// </summary>
    public const string SignatureAlgorithmId = "emh-1";

    /// <summary>
    /// The budget the <c>--test</c> history needs left before it is asked for (contract §7.3).
    /// </summary>
    public static readonly TimeSpan MinimumForRuns = TimeSpan.FromSeconds(1);

    private const string Unavailable = "Cloud data unavailable: ";
    private const string LoginRequiredReason = "your sign-in is no longer valid. Run xping login.";
    private const string CredentialTimeoutReason = "reading the Xping Cloud sign-in took too long. Showing local results only.";

    /// <summary>
    /// Returns <paramref name="envelope"/> with Cloud data added where it could be read.
    /// </summary>
    /// <remarks>
    /// Never throws except for the caller's own cancellation: whatever goes wrong here costs the
    /// Cloud data, never the local report or its exit code (contract §8.2 F1).
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
            // A defect here costs the Cloud data, and the agent still finds context.cloud.
            logger.LogInformation(ex, "Cloud enrichment failed: {Message}", ex.Message);
            var failed = new CloudContextDto("unavailable", "none", 0, null, null, null, null, null, null, 0, 0, [], []);
            return new EnrichmentResult(Unchanged(envelope, failed, "unavailable"), []);
        }
    }

    private async Task<EnrichmentResult> EnrichCoreAsync(
        ReportEnvelope envelope, EnrichmentRequest request, CancellationToken cancellationToken)
    {
        using var budget = new CloudBudget(timeProvider, cancellationToken);

        CliConfiguration configuration;
        try
        {
            configuration = configurationLoader.Load(request.Directory, request.ProjectFlag);
        }
        catch (CliConfigurationException ex)
        {
            return new EnrichmentResult(
                Unchanged(envelope, NotAttempted(null, null), "not-attempted"),
                [new CloudHint(Unavailable + ex.Message, Always: false)]);
        }

        string cloudUrl = configuration.CloudUrl.Value;

        // The keychain backends are synchronous native calls that never look at a token, and one
        // waiting on an access prompt would hold the report until someone answers it. The slice is
        // therefore applied around the resolution, not through it (§19 Q-2): the abandoned call
        // costs the Cloud data and ends with the process.
        ResolvedCredential credential;
        try
        {
            credential = await Task.Run(() => resolver.ResolveAsync(configuration, budget.CredentialToken), budget.CredentialToken)
                .WaitAsync(budget.CredentialToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (budget.CredentialExpired && !cancellationToken.IsCancellationRequested)
        {
            var timedOut = new CloudContextDto(
                "unavailable", "credential-timeout", Milliseconds(budget.Elapsed), cloudUrl, null, null, null, null, null, 0, 0, [], []);

            return new EnrichmentResult(
                Unchanged(envelope, timedOut, "unavailable"),
                [new CloudHint(Unavailable + CredentialTimeoutReason, Always: false)]);
        }

        logger.LogInformation("credential: {Credential}", Describe(credential));

        foreach (string problem in credential.StoreFailures.Concat(credential.Warnings))
            logger.LogInformation("{Problem}", problem);

        if (credential.Source == CredentialSource.None)
        {
            // A sign-in file that was refused or corrupt is the one silent "no credential" the user
            // can act on (§11.6); every other way of having none is the default local mode.
            return new EnrichmentResult(
                Unchanged(envelope, NotAttempted(cloudUrl, null), "not-attempted"),
                credential.Warnings.Count > 0 ? [new CloudHint(Unavailable + credential.Warnings[0], Always: false)] : []);
        }

        TargetPlan plan = TargetPlanner.Plan(envelope, request.Findings, request.Sessions);
        if (plan.Targets.Count == 0)
            return new EnrichmentResult(Unchanged(envelope, NotAttempted(cloudUrl, credential), "not-attempted"), []);

        var ledger = new Ledger();
        await AttemptAsync(credential, cloudUrl, configuration, request, envelope, plan, plan.Targets, budget, ledger).ConfigureAwait(false);

        bool loginRequired = ledger.CredentialFailure?.Status == "login-required";
        if (loginRequired && credential.Source == CredentialSource.StoredLogin)
        {
            ResolvedCredential fallback = credential.FallbackToApiKey();
            if (fallback.Source != CredentialSource.None)
            {
                logger.LogInformation(
                    "credential: fell back to API key ({Origin}) because the stored login is no longer valid", fallback.ApiKey?.Origin);

                credential = fallback;
                await AttemptAsync(
                    credential,
                    cloudUrl,
                    configuration,
                    request,
                    envelope,
                    plan,
                    [.. plan.Targets.Where(t => !ledger.Answers.ContainsKey(t.Fingerprint))],
                    budget,
                    ledger).ConfigureAwait(false);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<CloudProjectDto> projects = Projects(plan, ledger);
        CloudOutcome outcome = Outcome(plan, ledger, projects, cloudUrl, loginRequired);

        var context = new CloudContextDto(
            outcome.Status,
            outcome.Reason,
            Milliseconds(budget.Elapsed),
            cloudUrl,
            CredentialToken(credential),
            WorkspaceOf(credential),
            ledger.Answers.IsEmpty ? null : ledger.Answers.Values.Max(a => a.Digest.FetchedAt),
            ledger.SignatureAlgorithm,
            ledger.RetentionDays,
            plan.Targets.Count,
            ledger.Answers.Count,
            projects,
            [.. ledger.Sessions.OrderBy(s => s.ProjectKey, StringComparer.Ordinal).ThenBy(s => s.SessionKey, StringComparer.Ordinal)]);

        CloudTestHistoryDto? test = envelope.Test?.Test is null
            ? null
            : ledger.TestHistory ?? new CloudTestHistoryDto(
                envelope.Test.Test.Fingerprint is { } fingerprint && ledger.Answers.TryGetValue(fingerprint, out CloudAnswer? answer) ? answer.Digest : null,
                [],
                "unavailable",
                ledger.RetentionDays,
                HasMore: false);

        ReportEnvelope enriched = EnvelopeStitcher.Stitch(
            envelope,
            ledger.Answers,
            plan.Targets.ToDictionary(t => t.Fingerprint, t => t.LatestFailureHash, StringComparer.Ordinal),
            context,
            test);

        return new EnrichmentResult(enriched, outcome.Hint is { } hint ? [hint] : []);
    }

    /// <summary>
    /// Reads every target with one credential, until done, refused, or out of budget.
    /// </summary>
    private async Task AttemptAsync(
        ResolvedCredential credential,
        string cloudUrl,
        CliConfiguration configuration,
        EnrichmentRequest request,
        ReportEnvelope envelope,
        TargetPlan plan,
        IReadOnlyList<CloudTarget> targets,
        CloudBudget budget,
        Ledger ledger)
    {
        ledger.CredentialFailure = null;
        var lookup = new ProjectLookup(cloudUrl, WorkspaceOf(credential), configuration.ProjectId, request.SessionPins);

        try
        {
            using ICloudApiClient client = await clientFactory.CreateAsync(credential, cloudUrl, budget, budget.Token).ConfigureAwait(false);

            try
            {
                List<ProjectRead> projects = await BindAsync(client, credential, cloudUrl, targets, plan, lookup, budget, ledger).ConfigureAwait(false);
                IReadOnlyList<ProjectRead> missing = await ReadAsync(client, credential, cloudUrl, projects, budget, ledger).ConfigureAwait(false);

                if (missing.Count > 0)
                {
                    List<ProjectRead> rebound = await RebindAsync(client, missing, lookup, budget, ledger).ConfigureAwait(false);
                    await ReadAsync(client, credential, cloudUrl, rebound, budget, ledger).ConfigureAwait(false);
                }

                await ReadHistoryAsync(client, credential, cloudUrl, envelope, projects, budget, ledger).ConfigureAwait(false);
            }
            finally
            {
                foreach (string warning in client.Warnings)
                    logger.LogInformation("{Warning}", warning);
            }
        }
        catch (Exception ex) when (CredentialFailure(ex, credential) is { } failure)
        {
            ledger.CredentialFailure = failure;
        }
        catch (Exception ex) when (IsReadFailure(ex, budget))
        {
            // Before any project was bound: the client could not be built or the budget ran out.
            ledger.Lose(targets, Classify(ex, cloudUrl));
        }
    }

    /// <summary>
    /// Binds every assembly to a project and groups the targets by project key.
    /// </summary>
    private async Task<List<ProjectRead>> BindAsync(
        ICloudApiClient client,
        ResolvedCredential credential,
        string cloudUrl,
        IReadOnlyList<CloudTarget> targets,
        TargetPlan plan,
        ProjectLookup lookup,
        CloudBudget budget,
        Ledger ledger)
    {
        var bound = new List<(ProjectBinding Binding, string Assembly, CloudTarget[] Targets)>();

        foreach (IGrouping<string, CloudTarget> assembly in targets.GroupBy(t => t.Assembly, StringComparer.Ordinal))
        {
            ProjectBinding? binding;
            try
            {
                binding = await projectResolver.ResolveAsync(client, assembly.Key, lookup, budget.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (CredentialFailure(ex, credential) is null && IsReadFailure(ex, budget))
            {
                ledger.Lose([.. assembly], Classify(ex, cloudUrl));
                continue;
            }

            if (binding is null)
            {
                ledger.Rows[assembly.Key] = new Row(null, new Failure("not-found", $"no matching Cloud project for {assembly.Key}. Use --project <key>."));
                continue;
            }

            ledger.Rows[assembly.Key] = new Row(binding, null);
            bound.Add((binding, assembly.Key, [.. assembly]));
        }

        // Several assemblies can share a project; it is asked once, its session keys together.
        return
        [
            .. bound
                .GroupBy(b => b.Binding.Key, StringComparer.Ordinal)
                .Select(g => new ProjectRead(
                    g.First().Binding,
                    [.. g.Select(b => b.Assembly)],
                    [.. g.SelectMany(b => b.Targets)],
                    [
                        .. g.Select(b => plan.SessionKeys.GetValueOrDefault(b.Assembly))
                            .OfType<string>()
                            .Distinct(StringComparer.Ordinal)
                            .Take(MaxSessionKeysPerRequest)
                    ]))
        ];
    }

    /// <summary>
    /// Reads the digest of every target of every project, a batch per request, in one parallel loop.
    /// </summary>
    /// <returns>The projects the workspace does not have (the digest answered 404).</returns>
    /// <remarks>
    /// A failed batch loses only its own tests (cloud-data-in-cli-cli-spec §4.3). A refused
    /// credential is the one failure that ends every batch: it fails them all the same way.
    /// </remarks>
    private async Task<IReadOnlyList<ProjectRead>> ReadAsync(
        ICloudApiClient client,
        ResolvedCredential credential,
        string cloudUrl,
        IReadOnlyList<ProjectRead> projects,
        CloudBudget budget,
        Ledger ledger)
    {
        var missing = new ConcurrentDictionary<string, ProjectRead>(StringComparer.Ordinal);

        // The session keys ride on a project's first batch only: the answer is the same for each.
        IEnumerable<(ProjectRead Project, CloudTarget[] Targets, IReadOnlyList<string> SessionKeys)> batches = projects.SelectMany(
            p => p.Targets.Chunk(MaxDigestBatchSize).Select((chunk, i) => (p, chunk, i == 0 ? p.SessionKeys : (IReadOnlyList<string>)[])));

        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = budget.Token };

        try
        {
            await Parallel.ForEachAsync(batches, options, async (batch, token) =>
            {
                var digestRequest = new DigestRequest(
                    [.. batch.Targets.Select(t => new DigestRequestTest(t.Fingerprint, t.Hashes))], batch.SessionKeys);

                DigestResponse? response;
                try
                {
                    response = await client.GetDigestAsync(batch.Project.Binding.Key, digestRequest, token).ConfigureAwait(false);
                }
                catch (Exception ex) when (CredentialFailure(ex, credential) is null && IsReadFailure(ex, budget))
                {
                    ledger.Lose(batch.Targets, Classify(ex, cloudUrl));
                    return;
                }

                if (response is null)
                {
                    missing.TryAdd(batch.Project.Binding.Key, batch.Project);
                    return;
                }

                ledger.Record(batch.Project.Binding.Key, batch.Targets, response, timeProvider.GetUtcNow());
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (budget.Token.IsCancellationRequested)
        {
            // The batches never started read as the budget running out, their default.
        }

        return [.. missing.Values.OrderBy(p => p.Binding.Key, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Lists a remembered name match again (§11.2); any other key the workspace lacks is reported,
    /// because a typo in a key the user typed or the SDK recorded must not read as "Cloud has no data".
    /// </summary>
    private async Task<List<ProjectRead>> RebindAsync(
        ICloudApiClient client, IReadOnlyList<ProjectRead> missing, ProjectLookup lookup, CloudBudget budget, Ledger ledger)
    {
        var rebound = new List<ProjectRead>();

        foreach (ProjectRead project in missing)
        {
            ProjectBinding stale = project.Binding;
            if (!stale.IsNameMatch)
            {
                foreach (string assembly in project.Assemblies)
                {
                    ledger.Rows[assembly] = new Row(
                        stale, new Failure("not-found", $"no Cloud project '{stale.Key}' (from {stale.Origin}). Check the key, or pass --project <key>."));
                }

                continue;
            }

            // A name match is per assembly, so a project bound by name has one.
            string only = project.Assemblies[0];
            ProjectBinding? fresh = await projectResolver.RebindAsync(client, only, stale, lookup, budget.Token).ConfigureAwait(false);
            if (fresh is null)
            {
                ledger.Rows[only] = new Row(null, new Failure("not-found", $"no matching Cloud project for {only}. Use --project <key>."));
                continue;
            }

            ledger.Rows[only] = new Row(fresh, null);
            rebound.Add(project with { Binding = fresh });
        }

        return rebound;
    }

    /// <summary>
    /// Reads the <c>--test</c> subject's run history, after every digest batch, if the budget allows.
    /// </summary>
    private async Task ReadHistoryAsync(
        ICloudApiClient client,
        ResolvedCredential credential,
        string cloudUrl,
        ReportEnvelope envelope,
        IReadOnlyList<ProjectRead> projects,
        CloudBudget budget,
        Ledger ledger)
    {
        if (envelope.Test?.Test?.Fingerprint is not { } fingerprint
            || ledger.TestHistory is not null
            || !ledger.Answers.TryGetValue(fingerprint, out CloudAnswer? answer)
            || ledger.Rows.GetValueOrDefault(envelope.Test.Test.Assembly ?? string.Empty)?.Binding is not { } binding)
        {
            return;
        }

        // Cloud does not know the test, so it has no runs to list.
        if (answer.Digest.State != "ok")
        {
            ledger.TestHistory = new CloudTestHistoryDto(answer.Digest, [], "ok", ledger.RetentionDays, HasMore: false);
            return;
        }

        if (!budget.HasAtLeast(MinimumForRuns))
        {
            ledger.TestHistory = new CloudTestHistoryDto(answer.Digest, [], "budget", ledger.RetentionDays, HasMore: false);
            return;
        }

        try
        {
            RunsResponse? runs = await client.GetRunsAsync(binding.Key, fingerprint, RunsLimit, budget.Token).ConfigureAwait(false);
            ledger.TestHistory = new CloudTestHistoryDto(
                answer.Digest,
                [.. (runs?.Runs ?? []).Select(EnvelopeStitcher.ToDto)],
                "ok",
                runs?.RetentionDays ?? ledger.RetentionDays,
                runs?.HasMore ?? false);
        }
        catch (Exception ex) when (CredentialFailure(ex, credential) is not null || IsReadFailure(ex, budget))
        {
            logger.LogInformation("Cloud run history unavailable: {Reason}", Classify(ex, cloudUrl).Message);
            ledger.TestHistory = new CloudTestHistoryDto(
                answer.Digest, [], budget.Token.IsCancellationRequested ? "budget" : "unavailable", ledger.RetentionDays, HasMore: false);
        }
    }

    /// <summary>
    /// One row per assembly asked about, in target order.
    /// </summary>
    private static List<CloudProjectDto> Projects(TargetPlan plan, Ledger ledger) =>
    [
        .. plan.Targets
            .GroupBy(t => t.Assembly, StringComparer.Ordinal)
            .Select(g =>
            {
                Row? row = ledger.Rows.GetValueOrDefault(g.Key);
                (string status, string reason) = RowState([.. g], row, ledger);
                return new CloudProjectDto(g.Key, row?.Binding?.Key, row?.Binding?.Source, status, reason);
            })
    ];

    private static (string Status, string Reason) RowState(IReadOnlyList<CloudTarget> targets, Row? row, Ledger ledger)
    {
        if (row?.NoProject is not null)
            return ("no-project", "not-found");

        int answered = targets.Count(t => ledger.Answers.ContainsKey(t.Fingerprint));
        if (answered == targets.Count)
        {
            if (targets.Any(t => ledger.Unreadable.ContainsKey(t.Fingerprint)))
                return ("partial", "invalid-response");

            return ("ok", targets.Any(t => ledger.Answers[t.Fingerprint].AlgorithmMismatch) ? "signature-algorithm" : "none");
        }

        if (answered == 0 && ledger.CredentialFailure is { } refused)
            return (refused.Status, "none");

        string reason = targets
            .Where(t => !ledger.Answers.ContainsKey(t.Fingerprint))
            .Select(t => ledger.Lost.GetValueOrDefault(t.Fingerprint)?.Reason)
            .FirstOrDefault(r => r is not null) ?? "budget";

        return (answered > 0 ? "partial" : "unavailable", reason);
    }

    /// <summary>
    /// The report's status and reason from the per-assembly rows, under the precedence
    /// <c>login-required</c> &gt; <c>forbidden</c> &gt; <c>no-project</c> &gt; <c>unavailable</c>
    /// &gt; <c>partial</c> &gt; <c>ok</c>, and the one hint line that goes with them.
    /// </summary>
    private static CloudOutcome Outcome(
        TargetPlan plan, Ledger ledger, IReadOnlyList<CloudProjectDto> projects, string cloudUrl, bool loginRequired)
    {
        // A sign-in that is over is said even when the API key answered in its place: it is the one
        // thing a human must do, and the next run without the key would show nothing.
        CloudHint? loginHint = loginRequired ? new CloudHint(Unavailable + LoginRequiredReason, Always: true) : null;

        if (ledger.CredentialFailure is { } refused)
        {
            return new CloudOutcome(
                refused.Status,
                "none",
                refused.Status == "login-required" ? loginHint ?? new CloudHint(Unavailable + LoginRequiredReason, Always: true) : new CloudHint(Unavailable + refused.Message, Always: true));
        }

        if (projects.All(p => p.Status == "ok"))
            return new CloudOutcome("ok", projects.Any(p => p.Reason == "signature-algorithm") ? "signature-algorithm" : "none", loginHint);

        if (!ledger.Answers.IsEmpty)
        {
            string reason = projects.First(p => p.Status != "ok").Reason;
            string text = string.Create(
                CultureInfo.InvariantCulture, $"Cloud data partial: {ledger.Answers.Count} of {plan.Targets.Count} tests answered ({reason}).");

            return new CloudOutcome("partial", reason, loginHint ?? new CloudHint(text, Always: false));
        }

        CloudProjectDto first = projects.FirstOrDefault(p => p.Status == "no-project") ?? projects.First(p => p.Status != "ok");
        string message = first.Status == "no-project"
            ? ledger.Rows[first.Assembly].NoProject!.Message
            : plan.Targets.Select(t => ledger.Lost.GetValueOrDefault(t.Fingerprint)?.Message).FirstOrDefault(m => m is not null)
                ?? $"could not reach {cloudUrl} (timeout). Showing local results only.";

        return new CloudOutcome(first.Status, first.Reason, loginHint ?? new CloudHint(Unavailable + message, Always: false));
    }

    /// <summary>
    /// The failures that end every request made with the credential, not just one batch.
    /// </summary>
    /// <remarks>
    /// A plain 401 on a stored login is not one: without <c>invalid_token</c> nothing says the
    /// sign-in is over (a proxy's page, a stripped challenge), so it costs its batch like a network
    /// failure. An API key has no sign-in to be over; a 401 is the key refused.
    /// </remarks>
    private static RefusedCredential? CredentialFailure(Exception ex, ResolvedCredential credential) => ex switch
    {
        LoginRequiredException => new RefusedCredential("login-required", LoginRequiredReason),
        CloudApiException { StatusCode: 403 } refused => new RefusedCredential("forbidden", refused.Message),
        CloudApiException { StatusCode: 401 } when credential.Source != CredentialSource.StoredLogin
            => new RefusedCredential("login-required", LoginRequiredReason),
        _ => null
    };

    /// <summary>
    /// Whether a failure is the Cloud's or the network's, and costs the Cloud data; anything else is
    /// a defect and reaches the fail-open wrapper.
    /// </summary>
    private static bool IsReadFailure(Exception ex, CloudBudget budget) =>
        ex is CloudApiException or CloudResponseException or AuthFailureException
        || (ex is OperationCanceledException && budget.Token.IsCancellationRequested);

    /// <summary>
    /// The contract §8.1 reason for one read failure, and the words of its hint.
    /// </summary>
    private static Failure Classify(Exception ex, string cloudUrl) => ex switch
    {
        OperationCanceledException => new Failure("budget", $"could not reach {cloudUrl} (timeout). Showing local results only."),
        CloudResponseException => new Failure("invalid-response", ex.Message),
        CloudApiException { StatusCode: 429 } => new Failure("rate-limited", ex.Message),
        CloudApiException { StatusCode: >= 500 } server => new Failure(
            "server",
            string.Create(CultureInfo.InvariantCulture, $"could not reach {cloudUrl} (HTTP {server.StatusCode}). Showing local results only.")),
        CloudApiException => new Failure("server", ex.Message),
        AuthFailureException { ErrorCode: AuthErrorCodes.CloudUnreachable } unreachable => new Failure(
            unreachable.Reason == "timeout" ? "timeout" : "network",
            $"could not reach {unreachable.Target ?? cloudUrl} ({unreachable.Reason ?? "no answer"}). Showing local results only."),
        AuthFailureException { ErrorCode: AuthErrorCodes.VersionMismatch } => new Failure("server", ex.Message),
        _ => new Failure("network", ex.Message)
    };

    /// <summary>
    /// The envelope with only its Cloud context set: nothing was answered.
    /// </summary>
    private static ReportEnvelope Unchanged(ReportEnvelope envelope, CloudContextDto context, string runsState) =>
        EnvelopeStitcher.Stitch(
            envelope,
            new Dictionary<string, CloudAnswer>(StringComparer.Ordinal),
            new Dictionary<string, string?>(StringComparer.Ordinal),
            context,
            envelope.Test?.Test is null ? null : new CloudTestHistoryDto(null, [], runsState, null, HasMore: false));

    private static CloudContextDto NotAttempted(string? cloudUrl, ResolvedCredential? credential) =>
        new(
            "not-attempted",
            "none",
            ElapsedMs: 0,
            cloudUrl,
            credential is null ? null : CredentialToken(credential),
            credential is null ? null : WorkspaceOf(credential),
            null,
            null,
            null,
            0,
            0,
            [],
            []);

    private static long Milliseconds(TimeSpan elapsed) => (long)elapsed.TotalMilliseconds;

    private static string CredentialToken(ResolvedCredential credential) =>
        credential.Source == CredentialSource.StoredLogin ? "login" : "api-key";

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

    /// <summary>One project's tests, from the assemblies bound to it.</summary>
    private sealed record ProjectRead(
        ProjectBinding Binding, IReadOnlyList<string> Assemblies, IReadOnlyList<CloudTarget> Targets, IReadOnlyList<string> SessionKeys);

    /// <summary>An assembly's project, or why it has none.</summary>
    private sealed record Row(ProjectBinding? Binding, Failure? NoProject);

    /// <summary>Why a test went unanswered: a contract reason and the words of its hint.</summary>
    private sealed record Failure(string Reason, string Message);

    /// <summary>A credential every request was refused with.</summary>
    private sealed record RefusedCredential(string Status, string Message);

    /// <summary>
    /// What every attempt of one enrichment learned. Shared across the login-required fallback, so
    /// the API key asks only for what the sign-in left unanswered.
    /// </summary>
    private sealed class Ledger
    {
        private readonly Lock _gate = new();

        /// <summary>Gets Cloud's answers, by fingerprint.</summary>
        public ConcurrentDictionary<string, CloudAnswer> Answers { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets why each unanswered test went unanswered.</summary>
        public ConcurrentDictionary<string, Failure> Lost { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the tests Cloud answered in a state this CLI cannot read (contract §8.2 F4).</summary>
        public ConcurrentDictionary<string, bool> Unreadable { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets each assembly's project.</summary>
        public ConcurrentDictionary<string, Row> Rows { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets the session states Cloud answered.</summary>
        public ConcurrentBag<CloudSessionDto> Sessions { get; } = [];

        public RefusedCredential? CredentialFailure { get; set; }

        public CloudTestHistoryDto? TestHistory { get; set; }

        public string? SignatureAlgorithm { get; private set; }

        public int? RetentionDays { get; private set; }

        public void Lose(IEnumerable<CloudTarget> targets, Failure failure)
        {
            foreach (CloudTarget target in targets)
                Lost[target.Fingerprint] = failure;
        }

        /// <summary>
        /// Keeps one batch's answer. Only what was asked counts: a stray fingerprint must not make up
        /// for a missing one, and a requested one left out is lost as unreadable.
        /// </summary>
        public void Record(string projectKey, IReadOnlyList<CloudTarget> batch, DigestResponse response, DateTimeOffset fetchedAt)
        {
            bool mismatch = !string.Equals(response.SignatureAlgorithm, SignatureAlgorithmId, StringComparison.Ordinal);

            lock (_gate)
            {
                // A mismatch is the value worth reporting: it is why every match reads unknown.
                if (SignatureAlgorithm is null || mismatch)
                    SignatureAlgorithm = response.SignatureAlgorithm;

                RetentionDays ??= response.RetentionDays;
            }

            Dictionary<string, TestDigest> answered = response.Tests
                .Where(t => t is { Fingerprint: not null })
                .DistinctBy(t => t.Fingerprint, StringComparer.Ordinal)
                .ToDictionary(t => t.Fingerprint, StringComparer.Ordinal);

            foreach (CloudTarget target in batch)
            {
                if (!answered.TryGetValue(target.Fingerprint, out TestDigest? digest))
                {
                    Lost[target.Fingerprint] = new Failure("invalid-response", "Xping Cloud left tests out of its answer.");
                    continue;
                }

                if (digest.State is not ("ok" or "unknown"))
                    Unreadable[target.Fingerprint] = true;

                Answers[target.Fingerprint] = new CloudAnswer(EnvelopeStitcher.ToDto(digest, fetchedAt), mismatch);
                Lost.TryRemove(target.Fingerprint, out _);
            }

            foreach (DigestSession session in (response.Sessions ?? []).Where(s => s is { SessionKey.Length: > 0, State.Length: > 0 }))
                Sessions.Add(new CloudSessionDto(session.SessionKey, projectKey, session.State, session.Scored ?? false, session.FlakyExamined ?? false));
        }
    }
}
