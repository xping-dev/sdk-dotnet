/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Browser;
using Xping.Cli.Auth.Store;
using Xping.Cli.Configuration;
using Xping.Cli.Hosting;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// Runs the real <c>xping</c> command line in-process against a <see cref="FakeCloud"/>
/// (cli-auth-cli-spec §18.2).
/// </summary>
/// <remarks>
/// Everything that reaches outside the process is replaced: the clock, <c>~/.xping</c>, the
/// environment (CI runners set <c>CI=true</c>, which <c>login</c> refuses), the browser and the
/// headless detection.
/// </remarks>
internal sealed class CliFlowHost : IAsyncDisposable
{
    public CliFlowHost()
    {
        Time = new TimerTrackingTimeProvider(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
        Cloud = new FakeCloud(Time);
        HomeDirectory = Path.Combine(Path.GetTempPath(), "xping-cli-flow-tests", Guid.NewGuid().ToString("N"), ".xping");
        Browser = new FakeBrowser();
    }

    public TimerTrackingTimeProvider Time { get; }

    public FakeCloud Cloud { get; }

    public FakeBrowser Browser { get; }

    public string HomeDirectory { get; }

    public Dictionary<string, string> Environment { get; } = new(StringComparer.Ordinal);

    public BrowserEnvironment BrowserEnvironment { get; set; } = BrowserEnvironment.Interactive;

    public bool IsTerminal { get; set; } = true;

    /// <summary>
    /// Gets or sets the OS keychain the CLI sees; none by default, so a flow test never reads or
    /// writes the developer's real keychain.
    /// </summary>
    public IKeychainCredentialStore? Keychain { get; set; }

    /// <summary>
    /// Gets or sets whether <c>--cloud-url</c> is added to every command line; off for tests about
    /// the other sources of the Cloud URL.
    /// </summary>
    public bool PassCloudUrl { get; set; } = true;

    public string CredentialsFile => Path.Combine(HomeDirectory, "credentials.json");

    /// <summary>
    /// Runs <paramref name="args"/> with <c>--cloud-url</c> pointing at the fake.
    /// </summary>
    public CliResult Run(params string[] args) => Run(cancelKeys: null, args);

    public CliResult Run(CancelKeyHandler? cancelKeys, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(
            PassCloudUrl ? [.. args, "--cloud-url", Cloud.CloudUrl] : args,
            output,
            error,
            input: null,
            IsTerminal,
            Configure,
            cancelKeys);

        return new CliResult(code, output.ToString(), error.ToString());
    }

    /// <summary>
    /// Starts <paramref name="args"/> on a thread of its own, for a test that acts while it runs.
    /// </summary>
    public Task<CliResult> Start(params string[] args) => Task.Run(() => Run(args));

    /// <summary>
    /// Signs in through the loopback flow, for tests about what comes after.
    /// </summary>
    public CredentialRecord SignIn()
    {
        CliResult result = Run("login");
        Assert.True(result.Code == 0, result.Error);

        // The login's own waits (the 5-minute loopback timeout) are over; a test that drives the
        // clock afterwards must not jump by them.
        Time.DiscardCreatedTimers();
        return StoredRecord() ?? throw new InvalidOperationException("The sign-in was not stored.");
    }

    /// <summary>
    /// Writes <paramref name="content"/> as the credentials file, readable only by the user, as the
    /// CLI would create it.
    /// </summary>
    public void WriteCredentialsFile(string content)
    {
        Directory.CreateDirectory(HomeDirectory);
        File.WriteAllText(CredentialsFile, content);

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(CredentialsFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>
    /// Waits until the command under test starts a timer of <paramref name="dueTime"/>.
    /// </summary>
    public async Task WaitForTimerAsync(TimeSpan dueTime)
    {
        using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        while (true)
        {
            TimeSpan created = await Time.CreatedTimers.ReadAsync(patience.Token).ConfigureAwait(false);
            if (created == dueTime)
                return;
        }
    }

    /// <summary>
    /// Reads the stored sign-in for the fake's Cloud URL, as the CLI would.
    /// </summary>
    public CredentialRecord? StoredRecord()
    {
        using ServiceProvider services = BuildServices();
        return services.GetRequiredService<CredentialStoreSelector>().Select(Cloud.CloudUrl)
            .ReadAsync(Cloud.CloudUrl, CancellationToken.None).GetAwaiter().GetResult().Login?.Record;
    }

    /// <summary>
    /// Resolves the credential a command would use, with <paramref name="apiKeyFlag"/> as <c>--api-key</c>.
    /// </summary>
    public Task<ResolvedCredential> ResolveAsync(IServiceProvider services, string? apiKeyFlag = null)
    {
        CliConfiguration configuration = CliConfiguration.Load(
            Cloud.CloudUrl, apiKeyFlag, HomeDirectory, name => Environment.GetValueOrDefault(name));

        return services.GetRequiredService<CredentialResolver>().ResolveAsync(configuration, CancellationToken.None);
    }

    /// <summary>
    /// Waits for <paramref name="task"/>, moving the fake clock past each wait it starts
    /// (<see cref="ClockDriver"/>).
    /// </summary>
    public Task<T> DriveAsync<T>(Task<T> task) => ClockDriver.DriveAsync(Time, task);

    public async ValueTask DisposeAsync()
    {
        await Cloud.DisposeAsync().ConfigureAwait(false);

        string scratch = Path.GetDirectoryName(HomeDirectory)!;
        if (Directory.Exists(scratch))
            Directory.Delete(scratch, recursive: true);
    }

    /// <summary>
    /// Builds the services one <c>xping</c> process would have, for a test that drives them directly.
    /// </summary>
    public ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXpingCliServices(TextWriter.Null, TextWriter.Null, TextReader.Null, Terminals.All(false));
        Configure(services);
        return services.BuildServiceProvider();
    }

    private void Configure(IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(Time);
        services.AddSingleton(new XpingHome(HomeDirectory));
        services.AddSingleton<IEnvironmentVariableProvider>(new DictionaryEnvironment(Environment));
        services.AddSingleton<IHeadlessDetector>(new FixedHeadlessDetector(this));
        services.AddSingleton<IBrowserLauncher>(Browser);
        services.AddSingleton(provider => new CredentialStoreSelector(provider.GetRequiredService<FileCredentialStore>(), Keychain));
    }

    private sealed class DictionaryEnvironment(Dictionary<string, string> variables) : IEnvironmentVariableProvider
    {
        public string? GetVariable(string name) => variables.GetValueOrDefault(name);
    }

    private sealed class FixedHeadlessDetector(CliFlowHost host) : IHeadlessDetector
    {
        public BrowserEnvironment Detect() => host.BrowserEnvironment;
    }
}

/// <summary>
/// What one run of the command line produced.
/// </summary>
internal sealed record CliResult(int Code, string Output, string Error)
{
    /// <summary>
    /// Parses stdout as the command's single JSON document.
    /// </summary>
    public JsonElement Json()
    {
        using JsonDocument document = JsonDocument.Parse(Output);
        return document.RootElement.Clone();
    }
}
