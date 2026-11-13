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
using Xping.Cli.Report;
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
/// <param name="SubjectFor">
/// A test of the window by fingerprint, as the envelope shows it, or null when it is not in the
/// window. Null itself on a page that holds a selection (<c>--id</c>, <c>--test</c>): only the full
/// report asks the suite health (cloud-data-in-cli-cli-spec Amendment 7B).
/// </param>
/// <param name="ShowAll">Whether <c>--all</c> lifted the caps, the suite health's among them.</param>
internal sealed record EnrichmentRequest(
    string Directory,
    string? ProjectFlag,
    IReadOnlyDictionary<string, string> SessionPins,
    IReadOnlyList<TestSession> Sessions,
    IReadOnlyList<Finding> Findings,
    Func<string, SubjectDto?>? SubjectFor = null,
    bool ShowAll = false);

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
    /// The most tests on one page of the suite health (contract §6.5).
    /// </summary>
    public const int MaxHealthPageSize = 500;

    /// <summary>
    /// The most pages of one project's suite health a report reads.
    /// </summary>
    public const int MaxHealthPages = 10;

    /// <summary>
    /// The most suite-health rows a report lists without <c>--all</c>; the latest run's cap.
    /// </summary>
    public const int MaxHealthRows = LocalAnalysisConstants.LatestRunMaxRows;

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
    private const string RefusedKeyReason = "Xping Cloud does not accept this API key. Check the key, or sign in with `xping login`.";
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
            var failed = new CloudContextDto(
                "unavailable", "none", 0, null, null, null, null, null, null, null, 0, 0, [], [], HealthUnavailable(request, "none"));
            // Only the context is set: the defect may be in the stitcher, and running it again here
            // would throw past the one place that keeps Cloud from breaking the local report.
            return new EnrichmentResult(
                envelope with
                {
                    Context = envelope.Context is { } context ? context with { Cloud = failed } : new ContextDto(null, null, null, failed)
                },
                []);
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
                "unavailable",
                "credential-timeout",
                Milliseconds(budget.Elapsed),
                cloudUrl,
                null, null, null, null, null, null, 0, 0, [], [],
                HealthUnavailable(request, "credential-timeout"));

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

        // Bound from the sessions, not the targets: a clean run has no target, and its health is
        // exactly what a reader cannot get any other way (Amendment 7B).
        IReadOnlyList<string> healthAssemblies = request.SubjectFor is null
            ? []
            : [.. plan.SessionKeys.Keys.Order(StringComparer.Ordinal)];

        if (plan.Targets.Count == 0 && healthAssemblies.Count == 0)
            return new EnrichmentResult(Unchanged(envelope, NotAttempted(cloudUrl, credential), "not-attempted"), []);

        var ledger = new Ledger();
        await AttemptAsync(
            credential, cloudUrl, configuration, request, envelope, plan, plan.Targets, healthAssemblies, budget, ledger).ConfigureAwait(false);

        // A sign-in that is over, as opposed to a key refused: only a sign-in has a fallback.
        bool loginRequired = ledger.CredentialFailure?.Status == "login-required" && credential.Source == CredentialSource.StoredLogin;
        if (loginRequired)
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
                    healthAssemblies,
                    budget,
                    ledger).ConfigureAwait(false);

                // A fallback that answered nothing leaves the sign-in as the problem a person must fix
                // (§3.4): its own refusal or outage must not read as the reason there is no Cloud data.
                if (!ledger.AnythingAnswered)
                    ledger.CredentialFailure = new RefusedCredential("login-required", LoginRequiredReason);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<CloudProjectDto> projects = Projects(plan, healthAssemblies, ledger);
        CloudOutcome outcome = Outcome(plan, healthAssemblies, ledger, projects, cloudUrl, loginRequired);

        var context = new CloudContextDto(
            outcome.Status,
            outcome.Reason,
            Milliseconds(budget.Elapsed),
            cloudUrl,
            CredentialToken(credential),
            WorkspaceOf(credential),
            WorkspaceNameOf(credential),
            ledger.Answers.Values.Select(a => (DateTimeOffset?)a.Digest.FetchedAt)
                .Concat(ledger.Health.Values.Select(h => h.FetchedAt))
                .Max(),
            ledger.SignatureAlgorithm,
            ledger.RetentionDays,
            plan.Targets.Count,
            ledger.Answers.Count,
            projects,
            [.. ledger.Sessions.Values.OrderBy(s => s.ProjectKey, StringComparer.Ordinal).ThenBy(s => s.SessionKey, StringComparer.Ordinal)],
            Health(request, plan, healthAssemblies, ledger));

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
        IReadOnlyList<string> healthAssemblies,
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
                List<ProjectRead> projects = await BindAsync(
                    client, credential, cloudUrl, targets, healthAssemblies, plan, lookup, budget, ledger).ConfigureAwait(false);
                IReadOnlyList<ProjectRead> missing = await ReadAsync(client, credential, cloudUrl, projects, budget, ledger).ConfigureAwait(false);

                if (missing.Count > 0)
                {
                    List<ProjectRead> rebound = await RebindAsync(client, credential, cloudUrl, missing, lookup, budget, ledger).ConfigureAwait(false);
                    foreach (ProjectRead gone in await ReadAsync(client, credential, cloudUrl, rebound, budget, ledger).ConfigureAwait(false))
                    {
                        // Listed under its name just now, and still not readable: as good as absent.
                        foreach (string assembly in gone.Assemblies)
                            ledger.Rows[assembly] = new Row(gone.Binding, NoMatch(credential, assembly));
                    }
                }

                await ReadHistoryAsync(client, credential, cloudUrl, envelope, budget, ledger).ConfigureAwait(false);
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
            Failure failure = Classify(ex, cloudUrl);
            ledger.Lose(targets, failure);
            ledger.LoseHealth(healthAssemblies, failure);
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
        IReadOnlyList<string> healthAssemblies,
        TargetPlan plan,
        ProjectLookup lookup,
        CloudBudget budget,
        Ledger ledger)
    {
        var bound = new List<(ProjectBinding Binding, string Assembly, CloudTarget[] Targets)>();

        foreach ((string assembly, CloudTarget[] owned) in Assemblies(targets, healthAssemblies))
        {
            ProjectBinding? binding;
            try
            {
                binding = await projectResolver.ResolveAsync(client, assembly, lookup, budget.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (CredentialFailure(ex, credential) is null && IsReadFailure(ex, budget))
            {
                Failure failure = Classify(ex, cloudUrl);
                ledger.Lose(owned, failure);
                ledger.LoseHealth([.. healthAssemblies.Where(a => a == assembly)], failure);
                continue;
            }

            if (binding is null)
            {
                ledger.Rows[assembly] = new Row(null, NoMatch(credential, assembly));
                continue;
            }

            ledger.Rows[assembly] = new Row(binding, null);
            bound.Add((binding, assembly, owned));
        }

        // Several assemblies can share a project; it is asked once, its session keys together. A name
        // match is kept apart from a key bound another way: only it may be listed again on a 404.
        return
        [
            .. bound
                .GroupBy(b => (b.Binding.Key, b.Binding.IsNameMatch))
                .Select(g => new ProjectRead(
                    g.First().Binding,
                    [.. g.Select(b => b.Assembly)],
                    [.. g.SelectMany(b => b.Targets)],
                    [
                        .. g.Select(b => plan.SessionKeys.GetValueOrDefault(b.Assembly))
                            .OfType<string>()
                            .Distinct(StringComparer.Ordinal)
                            .Take(MaxSessionKeysPerRequest)
                    ],
                    g.Any(b => healthAssemblies.Contains(b.Assembly))))
        ];
    }

    /// <summary>
    /// The assemblies to bind, each with its targets: the targets' in target order, then the ones
    /// only the suite health asks about.
    /// </summary>
    private static IEnumerable<(string Assembly, CloudTarget[] Targets)> Assemblies(
        IReadOnlyList<CloudTarget> targets, IReadOnlyList<string> healthAssemblies) =>
        targets
            .GroupBy(t => t.Assembly, StringComparer.Ordinal)
            .Select(g => (g.Key, g.ToArray()))
            .Concat(healthAssemblies
                .Where(a => !targets.Any(t => t.Assembly == a))
                .Select(a => (a, Array.Empty<CloudTarget>())));

    /// <summary>
    /// Reads the suite health of every project that asks for it, then the digest of every target of
    /// every project, a batch per request, in one parallel loop.
    /// </summary>
    /// <returns>The projects the workspace does not have (the digest or the health answered 404).</returns>
    /// <remarks>
    /// A failed batch loses only its own tests (cloud-data-in-cli-cli-spec §4.3), and a failed health
    /// read only that project's health. A refused credential is the one failure that ends every
    /// request: it fails them all the same way. The health is scheduled first so a slow digest cannot
    /// starve it of the budget (Amendment 7C).
    /// </remarks>
    private async Task<IReadOnlyList<ProjectRead>> ReadAsync(
        ICloudApiClient client,
        ResolvedCredential credential,
        string cloudUrl,
        IReadOnlyList<ProjectRead> projects,
        CloudBudget budget,
        Ledger ledger)
    {
        var missing = new ConcurrentDictionary<(string Key, bool IsNameMatch), ProjectRead>();

        // Asked again on the fallback only where the first credential did not finish it.
        bool AsksHealth(ProjectRead p) =>
            p.Health && !p.Assemblies.All(a => ledger.Health.TryGetValue(a, out HealthRead? read) && read.Complete);

        // The session keys ride on one request per project: the answer is the same for each. The
        // health's first page when it is asked, else the first batch.
        IEnumerable<(ProjectRead Project, CloudTarget[]? Targets, IReadOnlyList<string> SessionKeys)> work =
        [
            .. projects.Where(AsksHealth).Select(p => (p, (CloudTarget[]?)null, p.SessionKeys)),
            .. projects.SelectMany(p => p.Targets.Chunk(MaxDigestBatchSize).Select(
                (chunk, i) => (p, (CloudTarget[]?)chunk, i == 0 && !AsksHealth(p) ? p.SessionKeys : (IReadOnlyList<string>)[])))
        ];

        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = budget.Token };

        try
        {
            await Parallel.ForEachAsync(work, options, async (item, token) =>
            {
                if (item.Targets is not { } targets)
                {
                    if (!await ReadHealthAsync(client, credential, cloudUrl, item.Project, budget, ledger, token).ConfigureAwait(false))
                        missing.TryAdd((item.Project.Binding.Key, item.Project.Binding.IsNameMatch), item.Project);

                    return;
                }

                var digestRequest = new DigestRequest(
                    [.. targets.Select(t => new DigestRequestTest(t.Fingerprint, t.Hashes))], item.SessionKeys);

                DigestResponse? response;
                try
                {
                    response = await client.GetDigestAsync(item.Project.Binding.Key, digestRequest, token).ConfigureAwait(false);
                }
                catch (Exception ex) when (CredentialFailure(ex, credential) is null && IsReadFailure(ex, budget))
                {
                    ledger.Lose(targets, Classify(ex, cloudUrl));
                    return;
                }

                if (response is null)
                {
                    missing.TryAdd((item.Project.Binding.Key, item.Project.Binding.IsNameMatch), item.Project);
                    return;
                }

                ledger.Record(item.Project.Binding.Key, targets, response, timeProvider.GetUtcNow());
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (budget.Token.IsCancellationRequested)
        {
            // The batches never started read as the budget running out, their default.
        }

        return [.. missing.Values.OrderBy(p => p.Binding.Key, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Reads one project's suite health, page after page, into the ledger of each of its assemblies.
    /// </summary>
    /// <returns>False when the workspace has no such project (the health answered 404).</returns>
    private async Task<bool> ReadHealthAsync(
        ICloudApiClient client,
        ResolvedCredential credential,
        string cloudUrl,
        ProjectRead project,
        CloudBudget budget,
        Ledger ledger,
        CancellationToken cancellationToken)
    {
        // Ledgered before the first page, so pages read before the budget ran out still count.
        var read = new HealthRead();
        foreach (string assembly in project.Assemblies)
            ledger.Health[assembly] = read;

        string? cursor = null;
        try
        {
            do
            {
                HealthResponse? page = await client.GetHealthAsync(
                    project.Binding.Key, read.Pages == 0 ? project.SessionKeys : [], cursor, cancellationToken).ConfigureAwait(false);

                if (page is null)
                {
                    foreach (string assembly in project.Assemblies)
                        ledger.Health.TryRemove(assembly, out _);

                    return false;
                }

                ledger.Record(project.Binding.Key, read, page, timeProvider.GetUtcNow());
                cursor = page.Next;
            }
            while (cursor is not null && read.Pages < MaxHealthPages);

            // More pages than a report reads: a suite this unhealthy is reported as partial, not
            // as though the rest were healthy.
            read.Complete = cursor is null;
            if (!read.Complete)
                read.Failure = new Failure("budget", "Xping Cloud's suite health has more pages than a report reads.");
        }
        catch (Exception ex) when (CredentialFailure(ex, credential) is null && IsReadFailure(ex, budget))
        {
            read.Failure = Classify(ex, cloudUrl);
        }

        return true;
    }

    /// <summary>
    /// Lists a remembered name match again (§11.2); any other key the workspace lacks is reported,
    /// because a typo in a key the user typed or the SDK recorded must not read as "Cloud has no data".
    /// </summary>
    private async Task<List<ProjectRead>> RebindAsync(
        ICloudApiClient client,
        ResolvedCredential credential,
        string cloudUrl,
        IReadOnlyList<ProjectRead> missing,
        ProjectLookup lookup,
        CloudBudget budget,
        Ledger ledger)
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
                        stale, NoProjectUnder(credential, stale));
                }

                continue;
            }

            // A name match is per assembly, so each is listed again on its own.
            foreach (string assembly in project.Assemblies)
            {
                CloudTarget[] targets = [.. project.Targets.Where(t => t.Assembly == assembly)];
                ProjectBinding? fresh;
                try
                {
                    fresh = await projectResolver.RebindAsync(client, assembly, stale, lookup, budget.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (CredentialFailure(ex, credential) is null && IsReadFailure(ex, budget))
                {
                    ledger.Lose(targets, Classify(ex, cloudUrl));
                    continue;
                }

                if (fresh is null)
                {
                    ledger.Rows[assembly] = new Row(null, NoMatch(credential, assembly));
                    continue;
                }

                ledger.Rows[assembly] = new Row(fresh, null);
                rebound.Add(project with { Binding = fresh, Assemblies = [assembly], Targets = targets });
            }
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
    /// One row per assembly asked about: the targets' in target order, then the ones only the suite
    /// health asked about.
    /// </summary>
    private static List<CloudProjectDto> Projects(TargetPlan plan, IReadOnlyList<string> healthAssemblies, Ledger ledger) =>
    [
        .. Assemblies(plan.Targets, healthAssemblies).Select(a =>
        {
            Row? row = ledger.Rows.GetValueOrDefault(a.Assembly);
            (string status, string reason) = RowState(a.Assembly, a.Targets, healthAssemblies.Contains(a.Assembly), row, ledger);
            return new CloudProjectDto(a.Assembly, row?.Binding?.Key, row?.Binding?.Source, status, reason);
        })
    ];

    /// <summary>
    /// A row is <c>ok</c> when every target answered and, where the health was asked, it read every
    /// page (Amendment 7F).
    /// </summary>
    private static (string Status, string Reason) RowState(
        string assembly, CloudTarget[] targets, bool healthAsked, Row? row, Ledger ledger)
    {
        if (row?.NoProject is not null)
            return ("no-project", "not-found");

        int answered = targets.Count(t => ledger.Answers.ContainsKey(t.Fingerprint));
        HealthRead? health = healthAsked ? ledger.Health.GetValueOrDefault(assembly) : null;
        bool healthShort = healthAsked && health?.Complete != true;

        if (answered == targets.Length && !healthShort)
        {
            if (targets.Any(t => ledger.Unreadable.ContainsKey(t.Fingerprint)))
                return ("partial", "invalid-response");

            return ("ok", targets.Any(t => ledger.Answers[t.Fingerprint].AlgorithmMismatch) ? "signature-algorithm" : "none");
        }

        bool anything = answered > 0 || health?.Pages > 0;
        if (!anything && ledger.CredentialFailure is { } refused)
            return (refused.Status, "none");

        string reason = answered < targets.Length
            ? targets
                .Where(t => !ledger.Answers.ContainsKey(t.Fingerprint))
                .Select(t => ledger.Lost.GetValueOrDefault(t.Fingerprint)?.Reason)
                .FirstOrDefault(r => r is not null) ?? "budget"
            : health?.Failure?.Reason ?? "budget";

        return (anything ? "partial" : "unavailable", reason);
    }

    /// <summary>
    /// The window's tests Cloud calls unhealthy (Amendment 7D, 7E): the health rows of the window's
    /// tests in each assembly read, less the ones the report already shows Cloud's verdict for.
    /// </summary>
    private static CloudHealthDto Health(
        EnrichmentRequest request, TargetPlan plan, IReadOnlyList<string> healthAssemblies, Ledger ledger)
    {
        if (request.SubjectFor is not { } subjectFor)
            return CloudHealthDto.NotAttempted;

        // An assembly with no project has no health to read; one whose binding failed is still asked.
        List<string> asked = [.. healthAssemblies.Where(a => ledger.Rows.GetValueOrDefault(a)?.NoProject is null)];
        if (asked.Count == 0)
            return CloudHealthDto.NotAttempted;

        List<HealthRead?> reads = [.. asked.Select(a => ledger.Health.GetValueOrDefault(a))];

        // The read a project's assemblies share is counted once.
        List<HealthRead> distinct = [.. reads.OfType<HealthRead>().Distinct()];
        string state = reads.All(r => r?.Complete == true) ? "ok"
            : reads.Any(r => r?.Pages > 0) ? "partial"
            : "unavailable";

        string reason = state == "ok"
            ? "none"
            : reads.FirstOrDefault(r => r?.Complete != true)?.Failure?.Reason ?? (ledger.CredentialFailure is null ? "budget" : "none");

        // Contract §9.7: a test Cloud knows that this window does not is never shown, and a test is
        // only its own assembly's. A fingerprint listed twice across pages is one test (§6.5).
        var kept = new Dictionary<string, CloudHealthTestDto>(StringComparer.Ordinal);
        foreach (string assembly in asked)
        {
            if (!ledger.Health.TryGetValue(assembly, out HealthRead? read))
                continue;

            foreach ((string fingerprint, CloudDigestDto digest) in read.Rows)
            {
                if (subjectFor(fingerprint) is { } subject && subject.Assembly == assembly)
                    kept.TryAdd(fingerprint, new CloudHealthTestDto(subject, digest));
            }
        }

        // A target already shows Cloud's verdict on its finding or its latest-run row.
        HashSet<string> elsewhere = [.. plan.Targets.Select(t => t.Fingerprint)];

        List<CloudHealthTestDto> listed =
        [
            .. kept
                .Where(k => !elsewhere.Contains(k.Key))
                .Select(k => k.Value)
                .OrderBy(t => VerdictRank(t.Digest.Verdict))
                .ThenBy(t => SeverityRank(t.Digest.Flaky?.Severity))
                .ThenBy(t => t.Subject.FullyQualifiedName, StringComparer.Ordinal)
                .ThenBy(t => t.Subject.Fingerprint, StringComparer.Ordinal)
        ];

        List<CloudHealthTestDto> shown = request.ShowAll ? listed : [.. listed.Take(MaxHealthRows)];

        return new CloudHealthDto(
            state,
            reason,
            distinct.Any(r => r.TotalTests is not null) ? distinct.Sum(r => r.TotalTests ?? 0) : null,
            kept.Count,
            kept.Count - listed.Count,
            shown,
            shown.Count,
            listed.Count);
    }

    private static int VerdictRank(string? verdict) => verdict switch
    {
        "broken" => 0,
        "known-flaky" => 1,
        "unstable" => 2,
        _ => 3
    };

    private static int SeverityRank(string? severity) => severity switch
    {
        "critical" => 0,
        "major" => 1,
        "moderate" => 2,
        "minor" => 3,
        _ => 4
    };

    /// <summary>
    /// The report's status and reason from the per-assembly rows, under the precedence
    /// <c>login-required</c> &gt; <c>forbidden</c> &gt; <c>no-project</c> &gt; <c>unavailable</c>
    /// &gt; <c>partial</c> &gt; <c>ok</c>, and the one hint line that goes with them.
    /// </summary>
    private static CloudOutcome Outcome(
        TargetPlan plan,
        IReadOnlyList<string> healthAssemblies,
        Ledger ledger,
        IReadOnlyList<CloudProjectDto> projects,
        string cloudUrl,
        bool loginRequired)
    {
        // A sign-in that is over is said even when the API key answered in its place: it is the one
        // thing a human must do, and the next run without the key would show nothing.
        CloudHint? loginHint = loginRequired ? new CloudHint(Unavailable + LoginRequiredReason, Always: true) : null;

        if (ledger.CredentialFailure is { } refused)
        {
            return new CloudOutcome(
                refused.Status,
                "none",
                loginHint ?? new CloudHint(Unavailable + refused.Message, Always: true));
        }

        if (projects.All(p => p.Status == "ok"))
            return new CloudOutcome("ok", projects.Any(p => p.Reason == "signature-algorithm") ? "signature-algorithm" : "none", loginHint);

        if (ledger.AnythingAnswered)
        {
            string reason = projects.First(p => p.Status != "ok").Reason;

            // Every target answered, so what is missing is the health alone.
            bool healthOnly = ledger.Answers.Count == plan.Targets.Count
                && healthAssemblies.Any(a => ledger.Health.GetValueOrDefault(a)?.Complete != true);

            string text = healthOnly
                ? $"Cloud data partial: suite health not complete ({reason})."
                : string.Create(
                    CultureInfo.InvariantCulture, $"Cloud data partial: {ledger.Answers.Count} of {plan.Targets.Count} tests answered ({reason}).");

            return new CloudOutcome("partial", reason, loginHint ?? new CloudHint(text, Always: false));
        }

        CloudProjectDto first = projects.FirstOrDefault(p => p.Status == "no-project") ?? projects.First(p => p.Status != "ok");
        string message = first.Status == "no-project"
            ? ledger.Rows[first.Assembly].NoProject!.Message
            : plan.Targets.Select(t => ledger.Lost.GetValueOrDefault(t.Fingerprint)?.Message)
                .Concat(healthAssemblies.Select(a => ledger.Health.GetValueOrDefault(a)?.Failure?.Message))
                .FirstOrDefault(m => m is not null)
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
            => new RefusedCredential("login-required", RefusedKeyReason),
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
        CloudApiException { StatusCode: 401 } => new Failure("network", ex.Message),
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
            credential is null ? null : WorkspaceNameOf(credential),
            null,
            null,
            null,
            0,
            0,
            [],
            [],
            CloudHealthDto.NotAttempted);

    /// <summary>
    /// The health of an enrichment that ended before any read: unavailable where it would have
    /// been asked.
    /// </summary>
    private static CloudHealthDto HealthUnavailable(EnrichmentRequest request, string reason) =>
        request.SubjectFor is null ? CloudHealthDto.NotAttempted : new CloudHealthDto("unavailable", reason, null, 0, 0, [], 0, 0);

    private static long Milliseconds(TimeSpan elapsed) => (long)elapsed.TotalMilliseconds;

    private static string CredentialToken(ResolvedCredential credential) =>
        credential.Source == CredentialSource.StoredLogin ? "login" : "api-key";

    private static string? WorkspaceOf(ResolvedCredential credential) => credential.Login?.Record.WorkspaceId;

    private static string? WorkspaceNameOf(ResolvedCredential credential) => credential.Login?.Record.WorkspaceName;

    /// <summary>No project in the workspace carries the assembly's name.</summary>
    private static Failure NoMatch(ResolvedCredential credential, string assembly) =>
        new("not-found", WorkspaceNameOf(credential) is { } workspace
            ? $"no matching Cloud project for {assembly} in workspace {workspace}. Use --project <key>."
            : $"no matching Cloud project for {assembly}. Use --project <key>.");

    /// <summary>
    /// A key names no project. A key the user configured, under a sign-in, may belong to another
    /// workspace, so its hint is contract §9.5's, keeping where the key came from so a typo is
    /// still fixed at its source (Amendment 4B). It never says whether the project exists
    /// elsewhere. A key recorded with the runs was never typed, so it keeps the plain hint.
    /// </summary>
    private static Failure NoProjectUnder(ResolvedCredential credential, ProjectBinding binding) =>
        new("not-found", !binding.IsSessionPin && WorkspaceNameOf(credential) is { } workspace
            ? $"project {binding.Key} (from {binding.Origin}) is not in workspace {workspace}. Check the key, or run xping login to choose another workspace."
            : $"no Cloud project '{binding.Key}' (from {binding.Origin}). Check the key, or pass --project <key>.");

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

    /// <summary>One project's tests, from the assemblies bound to it, and whether its health is asked.</summary>
    private sealed record ProjectRead(
        ProjectBinding Binding,
        IReadOnlyList<string> Assemblies,
        IReadOnlyList<CloudTarget> Targets,
        IReadOnlyList<string> SessionKeys,
        bool Health);

    /// <summary>
    /// How far one project's suite health was read, shared by the assemblies bound to it. Written by
    /// the one work item reading it, and read only after the loop.
    /// </summary>
    private sealed class HealthRead
    {
        /// <summary>Gets the unhealthy tests read so far, by fingerprint.</summary>
        public Dictionary<string, CloudDigestDto> Rows { get; } = new(StringComparer.Ordinal);

        public int Pages { get; set; }

        /// <summary>Gets or sets whether the last page was read.</summary>
        public bool Complete { get; set; }

        /// <summary>Gets or sets why the read stopped short; null when it did not.</summary>
        public Failure? Failure { get; set; }

        /// <summary>Gets or sets the tests Cloud classified, from the first page.</summary>
        public int? TotalTests { get; set; }

        public DateTimeOffset? FetchedAt { get; set; }
    }

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

        /// <summary>Gets the suite health read for each assembly.</summary>
        public ConcurrentDictionary<string, HealthRead> Health { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets whether any test or any page of a suite health was answered.</summary>
        public bool AnythingAnswered => !Answers.IsEmpty || Health.Values.Any(h => h.Pages > 0);

        /// <summary>Gets the session states Cloud answered.</summary>
        public ConcurrentDictionary<(string SessionKey, string ProjectKey), CloudSessionDto> Sessions { get; } = new();

        public RefusedCredential? CredentialFailure { get; set; }

        public CloudTestHistoryDto? TestHistory { get; set; }

        public string? SignatureAlgorithm { get; private set; }

        public int? RetentionDays { get; private set; }

        /// <summary>
        /// Records a health that could not be read at all; one already read stays as it is.
        /// </summary>
        public void LoseHealth(IEnumerable<string> assemblies, Failure failure)
        {
            foreach (string assembly in assemblies)
                Health.TryAdd(assembly, new HealthRead { Failure = failure });
        }

        public void Lose(IEnumerable<CloudTarget> targets, Failure failure)
        {
            // A test already answered stays answered: a later failure in the same attempt is not its.
            foreach (CloudTarget target in targets.Where(t => !Answers.ContainsKey(t.Fingerprint)))
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

            RecordSessions(projectKey, response.Sessions);
        }

        /// <summary>
        /// Keeps one page of a suite health. A test Cloud sent in a state this CLI cannot read is
        /// left out: nothing here would show it.
        /// </summary>
        public void Record(string projectKey, HealthRead read, HealthResponse page, DateTimeOffset fetchedAt)
        {
            lock (_gate)
                RetentionDays ??= page.RetentionDays;

            read.Pages++;
            read.TotalTests ??= page.TotalTests;
            read.FetchedAt = fetchedAt;

            foreach (TestDigest digest in page.Tests.Where(t => t is { Fingerprint.Length: > 0, State: "ok" }))
                read.Rows.TryAdd(digest.Fingerprint, EnvelopeStitcher.ToDto(digest, fetchedAt));

            RecordSessions(projectKey, page.Sessions);
        }

        private void RecordSessions(string projectKey, IReadOnlyList<DigestSession>? sessions)
        {
            foreach (DigestSession session in (sessions ?? []).Where(s => s is { SessionKey.Length: > 0, State.Length: > 0 }))
                Sessions[(session.SessionKey, projectKey)] = new CloudSessionDto(session.SessionKey, projectKey, session.State, session.Scored ?? false, session.FlakyExamined ?? false);
        }
    }
}
