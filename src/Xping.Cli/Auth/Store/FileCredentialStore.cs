/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Cli.Auth.Store;

/// <summary>
/// Keeps sign-ins in <c>~/.xping/credentials.json</c>, one entry per Cloud URL, readable only by
/// the user (cli-auth-cli-spec §7.5).
/// </summary>
/// <remarks>
/// <para>
/// Used when no OS credential store is available. The file is replaced whole on every change, with
/// its final mode from the first byte, so a reader sees the old file or the new one and never part
/// of either.
/// </para>
/// <para>
/// Entries are kept as raw JSON and parsed one at a time when looked up. A change to one Cloud URL
/// carries every other entry over untouched, so a corrupt entry is never lost to a login for
/// another server (§7.7).
/// </para>
/// </remarks>
internal sealed class FileCredentialStore(XpingHome home, IXpingSerializer serializer) : ICredentialStore
{
    private const int FileSchemaVersion = 1;

    private const UnixFileMode GroupOrOther =
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    // Read-modify-write of one shared file. Across processes the last move wins; refresh, the only
    // frequent writer, is serialized by its own cross-process lock (§9.4).
    private readonly Lock _gate = new();

    /// <inheritdoc/>
    public CredentialStoreKind Kind => CredentialStoreKind.File;

    /// <inheritdoc/>
    public string DisplayName => XpingHome.Display(home.CredentialsFile);

    /// <inheritdoc/>
    public async Task<CredentialReadResult> ReadAsync(string cloudUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cloudUrl);

        string path = home.CredentialsFile;

        // A file others can read may have been read, or written, by them: its tokens are not
        // trusted, whatever they contain (contract §10.4).
        if (IsUnsafe(path))
            return new CredentialReadResult(null, RefusalMessage());

        byte[]? content = await ReadFileAsync(path, cancellationToken).ConfigureAwait(false);
        if (content is null)
            return CredentialReadResult.None;

        Dictionary<string, JsonElement>? entries = Parse(content);
        if (entries is null)
            return new CredentialReadResult(null, CorruptMessage(cloudUrl));

        if (!entries.TryGetValue(cloudUrl, out JsonElement entry))
            return CredentialReadResult.None;

        CredentialRecord? record = ParseRecord(entry);
        if (record is null || !record.IsValidFor(cloudUrl))
            return new CredentialReadResult(null, CorruptMessage(cloudUrl));

        Redaction.AddSecret(record.RefreshToken);
        Redaction.AddSecret(record.AccessToken);
        return new CredentialReadResult(record, null);
    }

    /// <inheritdoc/>
    public Task WriteAsync(CredentialRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        using JsonDocument document = JsonDocument.Parse(serializer.SerializeToUtf8Bytes(record));

        lock (_gate)
        {
            Dictionary<string, JsonElement> entries = LoadForUpdate() ?? new(StringComparer.Ordinal);
            entries[record.CloudUrl] = document.RootElement;
            Save(entries);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string cloudUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cloudUrl);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            string path = home.CredentialsFile;
            if (!File.Exists(path))
                return Task.FromResult(false);

            // A file this CLI cannot parse, or must not trust, is unusable for every Cloud URL;
            // signing out removes it.
            Dictionary<string, JsonElement>? entries = IsUnsafe(path) ? null : LoadForUpdate();
            if (entries is null)
            {
                Delete(path);
                return Task.FromResult(true);
            }

            bool existed = entries.Remove(cloudUrl);
            if (entries.Count == 0)
                Delete(path);
            else if (existed)
                Save(entries);

            return Task.FromResult(existed);
        }
    }

    /// <summary>
    /// Returns the entries a change starts from: the file's own, or none when the file is missing,
    /// cannot be parsed, or is readable by others.
    /// </summary>
    /// <remarks>
    /// Entries of an unsafe file are dropped, not carried over: the rewritten file is private, and
    /// the next read would trust entries another user could have written (§7.5).
    /// </remarks>
    private Dictionary<string, JsonElement>? LoadForUpdate()
    {
        string path = home.CredentialsFile;
        if (IsUnsafe(path))
            return new(StringComparer.Ordinal);

        byte[]? content;
        try
        {
            content = File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CredentialStoreException($"Could not read {DisplayName}: {ex.Message}", ex);
        }

        return content is null ? new(StringComparer.Ordinal) : Parse(content);
    }

    private void Save(Dictionary<string, JsonElement> entries)
    {
        var file = new CredentialsFileContent(FileSchemaVersion, entries);

        try
        {
            home.EnsurePrivate();
            PrivateFiles.WriteAtomically(home.CredentialsFile, serializer.SerializeToUtf8Bytes(file));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CredentialStoreException($"Could not write {DisplayName}: {ex.Message}", ex);
        }
    }

    private void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CredentialStoreException($"Could not delete {DisplayName}: {ex.Message}", ex);
        }
    }

    private async Task<byte[]?> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CredentialStoreException($"Could not read {DisplayName}: {ex.Message}", ex);
        }
    }

    private Dictionary<string, JsonElement>? Parse(byte[] content)
    {
        try
        {
            CredentialsFileContent? file = serializer.Deserialize<CredentialsFileContent>(content);
            return file is { SchemaVersion: FileSchemaVersion, Credentials: not null }
                ? new Dictionary<string, JsonElement>(file.Credentials, StringComparer.Ordinal)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private CredentialRecord? ParseRecord(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object)
            return null;

        try
        {
            return serializer.Deserialize<CredentialRecord>(entry.GetRawText());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsUnsafe(string path)
    {
        // Windows has no mode bits; the file gets an owner-only ACL when it is written.
        if (OperatingSystem.IsWindows())
            return false;

        try
        {
            return (File.GetUnixFileMode(path) & GroupOrOther) != 0;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    private string RefusalMessage()
    {
        string file = DisplayName;
        string directory = XpingHome.Display(home.Root);
        return $"Refusing to read {file} because other users can read it. " +
            $"Run `chmod 600 {file}` (and `chmod 700 {directory}`) and try again.";
    }

    private static string CorruptMessage(string cloudUrl) =>
        $"Stored credentials for {cloudUrl} are unreadable and will be replaced at the next `xping login`.";

    private sealed record CredentialsFileContent(int SchemaVersion, Dictionary<string, JsonElement>? Credentials);
}
