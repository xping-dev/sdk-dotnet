/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Runtime.InteropServices;
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
            return CredentialReadResult.Corrupt(cloudUrl);

        if (!entries.TryGetValue(cloudUrl, out JsonElement entry))
            return CredentialReadResult.None;

        return CredentialRecordCodec.Decode(JsonMarshal.GetRawUtf8Value(entry), cloudUrl, serializer);
    }

    /// <inheritdoc/>
    public Task WriteAsync(CredentialRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        using JsonDocument document = JsonDocument.Parse(serializer.SerializeToUtf8Bytes(record));

        lock (_gate)
        {
            // Entries of an unsafe file are dropped, not carried over: the rewritten file is
            // private, and the next read would trust entries another user could have written. A
            // file that does not parse is replaced (§7.5, §7.7).
            Snapshot snapshot = Load();
            Dictionary<string, JsonElement> entries = snapshot.State == FileState.Usable ? snapshot.Entries! : [];

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
            Snapshot snapshot = Load();

            switch (snapshot.State)
            {
                case FileState.Missing:
                    return Task.FromResult(false);

                // Nothing in it is trusted for any Cloud URL, and the user was told so on every read.
                case FileState.Unsafe:
                    Delete(home.CredentialsFile);
                    return Task.FromResult(true);

                // It may hold another Cloud URL's sign-in written by a newer CLI; only a login
                // replaces it (§7.7).
                case FileState.Unparseable:
                    return Task.FromResult(false);
            }

            Dictionary<string, JsonElement> entries = snapshot.Entries!;
            if (!entries.Remove(cloudUrl))
                return Task.FromResult(false);

            if (entries.Count == 0)
                Delete(home.CredentialsFile);
            else
                Save(entries);

            return Task.FromResult(true);
        }
    }

    private Snapshot Load()
    {
        string path = home.CredentialsFile;
        if (IsUnsafe(path))
            return new Snapshot(FileState.Unsafe, null);

        byte[]? content;
        try
        {
            using FileStream? stream = OpenForRead(path);
            if (stream is null)
                return new Snapshot(FileState.Missing, null);

            content = new byte[stream.Length];
            stream.ReadExactly(content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CredentialStoreException($"Could not read {DisplayName}: {ex.Message}", ex);
        }

        Dictionary<string, JsonElement>? entries = Parse(content);
        return entries is null
            ? new Snapshot(FileState.Unparseable, null)
            : new Snapshot(FileState.Usable, entries);
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
            using FileStream? stream = OpenForRead(path);
            if (stream is null)
                return null;

            byte[] content = new byte[stream.Length];
            await stream.ReadExactlyAsync(content, cancellationToken).ConfigureAwait(false);
            return content;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CredentialStoreException($"Could not read {DisplayName}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Opens the file for reading, or returns <see langword="null"/> when it does not exist.
    /// </summary>
    /// <remarks>
    /// Shared for delete as well as write: on Windows, replacing a file that a reader holds open
    /// without <see cref="FileShare.Delete"/> fails with a sharing violation, and a reader in one
    /// process must never make a writer in another lose a rotated refresh token.
    /// </remarks>
    internal static FileStream? OpenForRead(string path)
    {
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private Dictionary<string, JsonElement>? Parse(byte[] content)
    {
        try
        {
            CredentialsFileContent? file = serializer.Deserialize<CredentialsFileContent>(content);
            return file is { SchemaVersion: FileSchemaVersion, Credentials: not null } ? file.Credentials : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private bool IsUnsafe(string path)
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A ~/.xping the user cannot search (left root-owned by a `sudo xping login`).
            throw new CredentialStoreException($"Could not read {DisplayName}: {ex.Message}", ex);
        }
    }

    private string RefusalMessage()
    {
        string file = DisplayName;
        string directory = XpingHome.Display(home.Root);
        return $"Refusing to read {file} because other users can read it. " +
            $"Run `chmod 600 {file}` (and `chmod 700 {directory}`) and try again.";
    }

    private enum FileState
    {
        Missing,
        Unsafe,
        Unparseable,
        Usable
    }

    private sealed record Snapshot(FileState State, Dictionary<string, JsonElement>? Entries);

    private sealed record CredentialsFileContent(int SchemaVersion, Dictionary<string, JsonElement>? Credentials);
}
