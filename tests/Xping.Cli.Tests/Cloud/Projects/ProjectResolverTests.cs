/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xping.Cli.Auth;
using Xping.Cli.Cloud;
using Xping.Cli.Cloud.Projects;
using Xping.Cli.Configuration;
using Xping.Cli.Tests.Auth.Store;

namespace Xping.Cli.Tests.Cloud.Projects;

/// <summary>
/// The binding order of cli-auth-cli-spec §11.2, and the name-match cache behind its step 4.
/// </summary>
public sealed class ProjectResolverTests : IDisposable
{
    private const string CloudUrl = "https://tests.invalid";
    private const string Workspace = "01J8K2V6XN7Y0Q4R5S6T7V8V9X";
    private const string Assembly = "Shop.Tests";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "xping-project-tests", Guid.NewGuid().ToString("N"), ".xping");
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
    private readonly ListingClient _client = new();
    private readonly ProjectCache _cache;
    private readonly ProjectResolver _resolver;

    public ProjectResolverTests()
    {
        _cache = new ProjectCache(new XpingHome(_home), CredentialTestData.Serializer, _time, NullLogger<ProjectCache>.Instance);
        _resolver = new ProjectResolver(_cache);
    }

    public void Dispose()
    {
        _client.Dispose();

        try
        {
            Directory.Delete(Path.GetDirectoryName(_home)!, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Theory]
    [InlineData(nameof(ConfigurationSource.Flag), "flag")]
    [InlineData(nameof(ConfigurationSource.Environment), "env")]
    [InlineData(nameof(ConfigurationSource.AppSettings), "config")]
    public async Task AConfiguredKeyWinsAndCostsNoRequest(string source, string expected)
    {
        ProjectLookup lookup = Lookup(
            configured: new ConfiguredValue("configured", Enum.Parse<ConfigurationSource>(source), "origin"),
            pins: new() { [Assembly] = "pinned" });

        ProjectBinding? binding = await _resolver.ResolveAsync(_client, Assembly, lookup, CancellationToken.None);

        Assert.Equal(("configured", expected, "origin"), (binding!.Key, binding.Source, binding.Origin));
        Assert.Empty(_client.Pages);
    }

    [Fact]
    public async Task TheSessionPinComesNext()
    {
        ProjectBinding? binding = await _resolver.ResolveAsync(
            _client, Assembly, Lookup(pins: new() { [Assembly] = "pinned" }), CancellationToken.None);

        Assert.Equal(("pinned", "session"), (binding!.Key, binding.Source));
        Assert.Empty(_client.Pages);
    }

    [Fact]
    public async Task OtherwiseTheProjectWhoseDisplayNameIsTheAssemblyIsFoundPageByPage()
    {
        _client.Add(60, extra: new ProjectSummary("shop", Assembly, "shop"));

        ProjectBinding? binding = await _resolver.ResolveAsync(_client, Assembly, Lookup(), CancellationToken.None);

        Assert.Equal(("shop", ProjectResolver.NameMatch, false), (binding!.Key, binding.Source, binding.FromCache));
        Assert.Equal([1, 2], _client.Pages);
    }

    [Fact]
    public async Task TheNameMatchIsCachedForTheWorkspace()
    {
        _client.Add(0, extra: new ProjectSummary("shop", Assembly, "shop"));
        await _resolver.ResolveAsync(_client, Assembly, Lookup(), CancellationToken.None);

        ProjectBinding? again = await _resolver.ResolveAsync(_client, Assembly, Lookup(), CancellationToken.None);

        Assert.Equal("shop", again!.Key);
        Assert.Single(_client.Pages);
        Assert.True(File.Exists(Path.Combine(new XpingHome(_home).ProjectCacheDirectory(CloudUrl), Workspace + ".json")));
    }

    [Fact]
    public async Task ACachedMatchExpiresAfterADay()
    {
        _client.Add(0, extra: new ProjectSummary("shop", Assembly, "shop"));
        await _resolver.ResolveAsync(_client, Assembly, Lookup(), CancellationToken.None);

        _time.Advance(ProjectCache.MaxAge);
        await _resolver.ResolveAsync(_client, Assembly, Lookup(), CancellationToken.None);

        Assert.Equal(2, _client.Pages.Count);
    }

    [Fact]
    public async Task AnApiKeyHasNoWorkspaceSoNothingIsCached()
    {
        _client.Add(0, extra: new ProjectSummary("shop", Assembly, "shop"));

        await _resolver.ResolveAsync(_client, Assembly, Lookup(workspace: null), CancellationToken.None);
        await _resolver.ResolveAsync(_client, Assembly, Lookup(workspace: null), CancellationToken.None);

        Assert.Equal(2, _client.Pages.Count);
        Assert.False(Directory.Exists(_home));
    }

    [Fact]
    public async Task NoMatchingDisplayNameBindsNothing()
    {
        _client.Add(3);

        Assert.Null(await _resolver.ResolveAsync(_client, Assembly, Lookup(), CancellationToken.None));
    }

    [Fact]
    public async Task ARebindOfACachedMatchDropsItAndListsAgain()
    {
        _client.Add(0, extra: new ProjectSummary("old", Assembly, "old"));
        await _resolver.ResolveAsync(_client, Assembly, Lookup(), CancellationToken.None);
        ProjectBinding stale = (await _resolver.ResolveAsync(_client, Assembly, Lookup(), CancellationToken.None))!;
        Assert.True(stale.FromCache);

        _client.Projects.Clear();
        _client.Add(0, extra: new ProjectSummary("renamed", Assembly, "renamed"));
        ProjectBinding? fresh = await _resolver.RebindAsync(_client, Assembly, stale, Lookup(), CancellationToken.None);

        Assert.Equal("renamed", fresh!.Key);
        Assert.Equal("renamed", (await _resolver.ResolveAsync(_client, Assembly, Lookup(), CancellationToken.None))!.Key);
    }

    [Fact]
    public async Task ARebindThatFindsTheSameProjectGivesNothingNew()
    {
        _client.Add(0, extra: new ProjectSummary("shop", Assembly, "shop"));
        await _resolver.ResolveAsync(_client, Assembly, Lookup(), CancellationToken.None);
        ProjectBinding stale = (await _resolver.ResolveAsync(_client, Assembly, Lookup(), CancellationToken.None))!;

        Assert.Null(await _resolver.RebindAsync(_client, Assembly, stale, Lookup(), CancellationToken.None));
    }

    [Fact]
    public async Task AMatchListedInThisRunIsNotListedAgain()
    {
        _client.Add(0, extra: new ProjectSummary("shop", Assembly, "shop"));
        ProjectBinding listed = (await _resolver.ResolveAsync(_client, Assembly, Lookup(), CancellationToken.None))!;

        Assert.Null(await _resolver.RebindAsync(_client, Assembly, listed, Lookup(), CancellationToken.None));
        Assert.Single(_client.Pages);
        Assert.NotNull(_cache.Read(CloudUrl, Workspace, Assembly));
    }

    [Fact]
    public async Task OnlyANameMatchIsRebound()
    {
        Assert.Null(await _resolver.RebindAsync(
            _client, Assembly, new ProjectBinding("pinned", "session", "x"), Lookup(), CancellationToken.None));
        Assert.Empty(_client.Pages);
    }

    [Fact]
    public void AWorkspaceIdThatCannotBeAFileNameIsNeverCached()
    {
        _cache.Write(CloudUrl, "../escape", Assembly, new ProjectSummary("shop", Assembly, null));

        Assert.Null(_cache.Read(CloudUrl, "../escape", Assembly));
        Assert.False(Directory.Exists(_home));
    }

    [Fact]
    public void ACorruptCacheFileIsAMiss()
    {
        string directory = new XpingHome(_home).ProjectCacheDirectory(CloudUrl);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, Workspace + ".json"), "{ not json");

        Assert.Null(_cache.Read(CloudUrl, Workspace, Assembly));
    }

    private static ProjectLookup Lookup(
        ConfiguredValue? configured = null, Dictionary<string, string>? pins = null, string? workspace = Workspace) =>
        new(CloudUrl, workspace, configured, pins ?? []);

    private sealed class ListingClient : ICloudApiClient
    {
        public List<ProjectSummary> Projects { get; } = [];

        public List<int> Pages { get; } = [];

        public IReadOnlyList<string> Warnings => [];

        /// <summary>Adds <paramref name="count"/> unrelated projects, then <paramref name="extra"/>.</summary>
        public void Add(int count, ProjectSummary? extra = null)
        {
            Projects.AddRange(Enumerable.Range(0, count).Select(i => new ProjectSummary($"p{i}", $"Other{i}.Tests", null)));

            if (extra is not null)
                Projects.Add(extra);
        }

        public Task<PagedResult<ProjectSummary>> ListProjectsAsync(int pageNumber, int pageSize, CancellationToken cancellationToken)
        {
            Pages.Add(pageNumber);
            return Task.FromResult(new PagedResult<ProjectSummary>(
                [.. Projects.Skip((pageNumber - 1) * pageSize).Take(pageSize)], Projects.Count, pageNumber, pageSize));
        }

        public Task<ProjectSummary?> GetProjectAsync(string projectKey, CancellationToken cancellationToken) =>
            Task.FromResult(Projects.FirstOrDefault(p => p.Id == projectKey));

        public Task<CloudTest?> GetTestAsync(string projectKey, string testFingerprint, CancellationToken cancellationToken) =>
            Task.FromResult<CloudTest?>(null);

        public void Dispose()
        {
        }
    }
}
