/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Store;
using static Xping.Cli.Tests.Auth.Store.CredentialTestData;

namespace Xping.Cli.Tests.Auth.Store;

public sealed class FileCredentialStoreTests : IDisposable
{
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PrivateDirectory = PrivateFile | UnixFileMode.UserExecute;

    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "xping-cli-credential-store-tests", Guid.NewGuid().ToString("N"));

    private readonly XpingHome _home;
    private readonly FileCredentialStore _store;

    public FileCredentialStoreTests()
    {
        _home = new XpingHome(Path.Combine(_scratch, ".xping"));
        _store = new FileCredentialStore(_home, Serializer);
    }

    private string FilePath => _home.CredentialsFile;

    public void Dispose()
    {
        if (Directory.Exists(_scratch))
            Directory.Delete(_scratch, recursive: true);
    }

    [Fact]
    public async Task NothingIsFoundWhenNoFileExists()
    {
        CredentialReadResult result = await _store.ReadAsync(CloudUrl, CancellationToken.None);

        Assert.Equal(CredentialReadResult.None, result);
    }

    [Fact]
    public async Task AWrittenRecordReadsBackUnchanged()
    {
        await _store.WriteAsync(Record(), CancellationToken.None);

        CredentialReadResult result = await _store.ReadAsync(CloudUrl, CancellationToken.None);

        Assert.Equal(Record(), result.Record);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task OneFileHoldsASignInPerCloudUrl()
    {
        await _store.WriteAsync(Record(), CancellationToken.None);
        await _store.WriteAsync(Record(OtherCloudUrl, "other-refresh-token-0123"), CancellationToken.None);

        Assert.Equal(Record(), (await _store.ReadAsync(CloudUrl, CancellationToken.None)).Record);
        Assert.Equal(
            Record(OtherCloudUrl, "other-refresh-token-0123"),
            (await _store.ReadAsync(OtherCloudUrl, CancellationToken.None)).Record);

        using JsonDocument file = JsonDocument.Parse(await File.ReadAllTextAsync(FilePath));
        Assert.Equal(1, file.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(2, file.RootElement.GetProperty("credentials").EnumerateObject().Count());
    }

    [Fact]
    public async Task ReplacingOneSignInKeepsAnotherCloudUrlsEntryVerbatimEvenWhenCorrupt()
    {
        const string corrupt = """{"schemaVersion":7,"whatever":[1,2,3]}""";
        WriteFile($$$"""{"schemaVersion":1,"credentials":{"{{{OtherCloudUrl}}}":{{{corrupt}}}}}""");

        await _store.WriteAsync(Record(), CancellationToken.None);
        await _store.WriteAsync(Record(refreshToken: "rotated-refresh-token-01"), CancellationToken.None);

        using JsonDocument file = JsonDocument.Parse(await File.ReadAllTextAsync(FilePath));
        Assert.Equal(corrupt, file.RootElement.GetProperty("credentials").GetProperty(OtherCloudUrl).GetRawText());
        Assert.Equal("rotated-refresh-token-01", (await _store.ReadAsync(CloudUrl, CancellationToken.None)).Record?.RefreshToken);
    }

    [Fact]
    public async Task TheFileIsPrivateInAPrivateDirectoryAndNoTemporaryFileIsLeft()
    {
        if (OperatingSystem.IsWindows())
            return;

        await _store.WriteAsync(Record(), CancellationToken.None);

        Assert.Equal(PrivateFile, File.GetUnixFileMode(FilePath));
        Assert.Equal(PrivateDirectory, File.GetUnixFileMode(_home.Root));
        Assert.Equal([FilePath], Directory.GetFiles(_home.Root));
    }

    [Fact]
    public async Task AnExistingOpenXpingDirectoryIsTightenedBeforeTheFirstWrite()
    {
        if (OperatingSystem.IsWindows())
            return;

        // A home directory that is a repository already has a 0755 ~/.xping from the SDK's store.
        Directory.CreateDirectory(_home.Root);
        File.SetUnixFileMode(_home.Root, PrivateDirectory | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        await _store.WriteAsync(Record(), CancellationToken.None);

        Assert.Equal(PrivateDirectory, File.GetUnixFileMode(_home.Root));
    }

    [Fact]
    public async Task AFileOtherUsersCanReadIsRefusedWithInstructions()
    {
        if (OperatingSystem.IsWindows())
            return;

        await _store.WriteAsync(Record(), CancellationToken.None);
        File.SetUnixFileMode(FilePath, PrivateFile | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        CredentialReadResult result = await _store.ReadAsync(CloudUrl, CancellationToken.None);

        Assert.Null(result.Record);
        Assert.Equal(
            $"Refusing to read {XpingHome.Display(FilePath)} because other users can read it. " +
            $"Run `chmod 600 {XpingHome.Display(FilePath)}` (and `chmod 700 {XpingHome.Display(_home.Root)}`) and try again.",
            result.Warning);
    }

    [Fact]
    public async Task AFileOtherUsersCanWriteIsRefused()
    {
        if (OperatingSystem.IsWindows())
            return;

        await _store.WriteAsync(Record(), CancellationToken.None);
        File.SetUnixFileMode(FilePath, PrivateFile | UnixFileMode.GroupWrite);

        CredentialReadResult result = await _store.ReadAsync(CloudUrl, CancellationToken.None);

        Assert.Null(result.Record);
        Assert.StartsWith("Refusing to read", result.Warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WritingOverAnUnsafeFileDropsItsOtherEntriesAndMakesItPrivate()
    {
        if (OperatingSystem.IsWindows())
            return;

        // Carried over into a private file, an entry another user planted would be trusted.
        await _store.WriteAsync(Record(OtherCloudUrl, "planted-refresh-token-01"), CancellationToken.None);
        File.SetUnixFileMode(FilePath, PrivateFile | UnixFileMode.OtherWrite | UnixFileMode.OtherRead);

        await _store.WriteAsync(Record(), CancellationToken.None);

        Assert.Equal(PrivateFile, File.GetUnixFileMode(FilePath));
        Assert.Equal(Record(), (await _store.ReadAsync(CloudUrl, CancellationToken.None)).Record);
        Assert.Equal(CredentialReadResult.None, await _store.ReadAsync(OtherCloudUrl, CancellationToken.None));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"schemaVersion":2,"credentials":{"https://tests.invalid":{}}}""")]
    [InlineData("""{"schemaVersion":1}""")]
    [InlineData("""{"schemaVersion":1,"credentials":{"https://tests.invalid":"a string"}}""")]
    [InlineData("""{"schemaVersion":1,"credentials":{"https://tests.invalid":{"schemaVersion":"one"}}}""")]
    [InlineData("""{"schemaVersion":1,"credentials":{"https://tests.invalid":{"schemaVersion":2,"cloudUrl":"https://tests.invalid","refreshToken":"refresh-token-0123456789","dataGatewayUri":"https://api.tests.invalid"}}}""")]
    [InlineData("""{"schemaVersion":1,"credentials":{"https://tests.invalid":{"schemaVersion":1,"cloudUrl":"https://tests.invalid","dataGatewayUri":"https://api.tests.invalid"}}}""")]
    [InlineData("""{"schemaVersion":1,"credentials":{"https://tests.invalid":{"schemaVersion":1,"cloudUrl":"https://other.tests.invalid","refreshToken":"refresh-token-0123456789","dataGatewayUri":"https://api.tests.invalid"}}}""")]
    public async Task ACorruptEntryReadsAsNothingWithAWarningAndIsLeftInPlace(string content)
    {
        WriteFile(content);

        CredentialReadResult result = await _store.ReadAsync(CloudUrl, CancellationToken.None);

        Assert.Null(result.Record);
        Assert.Equal(
            "Stored credentials for https://tests.invalid are unreadable and will be replaced at the next `xping login`.",
            result.Warning);
        Assert.Equal(content, await File.ReadAllTextAsync(FilePath));
    }

    [Fact]
    public async Task ASignInReplacesAFileThatDoesNotParse()
    {
        WriteFile("not json");

        await _store.WriteAsync(Record(), CancellationToken.None);

        Assert.Equal(Record(), (await _store.ReadAsync(CloudUrl, CancellationToken.None)).Record);
    }

    [Fact]
    public async Task DeletingOneSignInKeepsTheOthers()
    {
        await _store.WriteAsync(Record(), CancellationToken.None);
        await _store.WriteAsync(Record(OtherCloudUrl), CancellationToken.None);

        Assert.True(await _store.DeleteAsync(CloudUrl, CancellationToken.None));

        Assert.Equal(CredentialReadResult.None, await _store.ReadAsync(CloudUrl, CancellationToken.None));
        Assert.NotNull((await _store.ReadAsync(OtherCloudUrl, CancellationToken.None)).Record);
    }

    [Fact]
    public async Task DeletingTheLastSignInRemovesTheFile()
    {
        await _store.WriteAsync(Record(), CancellationToken.None);

        Assert.True(await _store.DeleteAsync(CloudUrl, CancellationToken.None));

        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task DeletingASignInThatIsNotStoredReportsNothingDeleted()
    {
        Assert.False(await _store.DeleteAsync(CloudUrl, CancellationToken.None));

        await _store.WriteAsync(Record(OtherCloudUrl), CancellationToken.None);
        string before = await File.ReadAllTextAsync(FilePath);

        Assert.False(await _store.DeleteAsync(CloudUrl, CancellationToken.None));
        Assert.Equal(before, await File.ReadAllTextAsync(FilePath));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"schemaVersion":2,"credentials":{"https://other.tests.invalid":{"schemaVersion":2}}}""")]
    public async Task SigningOutLeavesAFileThatDoesNotParseUntouched(string content)
    {
        // It may hold another Cloud URL's sign-in written by a newer CLI. Before the fix, signing
        // out of any Cloud URL deleted it and reported a sign-out that never happened.
        WriteFile(content);

        Assert.False(await _store.DeleteAsync(CloudUrl, CancellationToken.None));

        Assert.Equal(content, await File.ReadAllTextAsync(FilePath));
    }

    [Fact]
    public async Task SigningOutRemovesAnUnsafeFile()
    {
        if (OperatingSystem.IsWindows())
            return;

        await _store.WriteAsync(Record(), CancellationToken.None);
        File.SetUnixFileMode(FilePath, PrivateFile | UnixFileMode.OtherRead);

        Assert.True(await _store.DeleteAsync(CloudUrl, CancellationToken.None));

        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task ConcurrentWritesForDifferentCloudUrlsAreAllKept()
    {
        string[] urls = [.. Enumerable.Range(0, 16).Select(i => $"https://cloud{i}.tests.invalid")];

        await Task.WhenAll(urls.Select(url => Task.Run(() => _store.WriteAsync(Record(url), CancellationToken.None))));

        foreach (string url in urls)
            Assert.NotNull((await _store.ReadAsync(url, CancellationToken.None)).Record);
    }

    [Fact]
    public async Task ReadingRegistersTheTokensForRedaction()
    {
        const string refresh = "redaction-refresh-token-0123";
        await _store.WriteAsync(Record(refreshToken: refresh), CancellationToken.None);

        await _store.ReadAsync(CloudUrl, CancellationToken.None);

        Assert.Equal($"token {Redaction.Placeholder}", Redaction.Scrub($"token {refresh}"));
    }

    [Fact]
    public async Task AnXpingDirectoryTheUserCannotSearchIsAStoreFailure()
    {
        // Root ignores the mode, so the failure cannot be produced as root (a container run).
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
            return;

        // Left behind by a `sudo xping login`. Before the fix, the stat in the mode check threw
        // UnauthorizedAccessException, which no caller expects.
        await _store.WriteAsync(Record(), CancellationToken.None);
        File.SetUnixFileMode(_home.Root, UnixFileMode.None);

        try
        {
            await Assert.ThrowsAsync<CredentialStoreException>(() => _store.ReadAsync(CloudUrl, CancellationToken.None));
            await Assert.ThrowsAsync<CredentialStoreException>(() => _store.WriteAsync(Record(), CancellationToken.None));
            await Assert.ThrowsAsync<CredentialStoreException>(() => _store.DeleteAsync(CloudUrl, CancellationToken.None));
        }
        finally
        {
            File.SetUnixFileMode(_home.Root, PrivateDirectory);
        }
    }

    [Fact]
    public async Task AWriteReplacesTheFileWhileAReaderHoldsItOpen()
    {
        // On Windows, a reader without FileShare.Delete made the replacing move fail with a sharing
        // violation, losing a rotated refresh token. Elsewhere this always held.
        await _store.WriteAsync(Record(), CancellationToken.None);

        using (FileStream? reader = FileCredentialStore.OpenForRead(FilePath))
        {
            Assert.NotNull(reader);
            await _store.WriteAsync(Record(refreshToken: "rotated-refresh-token-01"), CancellationToken.None);
        }

        Assert.Equal("rotated-refresh-token-01", (await _store.ReadAsync(CloudUrl, CancellationToken.None)).Record?.RefreshToken);
    }

    private void WriteFile(string content)
    {
        Directory.CreateDirectory(_home.Root);
        File.WriteAllText(FilePath, content);

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(FilePath, PrivateFile);
    }
}
