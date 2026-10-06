/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth.Store;

namespace Xping.Cli.Tests.Auth.Store;

/// <summary>
/// An in-memory store that stands in for a keychain or the file, with a scripted warning or failure.
/// </summary>
internal sealed class FakeCredentialStore(CredentialStoreKind kind, string displayName) : ICredentialStore
{
    private readonly Dictionary<string, CredentialRecord> _records = new(StringComparer.Ordinal);

    public CredentialStoreKind Kind { get; } = kind;

    public string DisplayName { get; } = displayName;

    public string? Warning { get; set; }

    public bool Fails { get; set; }

    public List<string> Deleted { get; } = [];

    public void Add(CredentialRecord record) => _records[record.CloudUrl] = record;

    public bool Contains(string cloudUrl) => _records.ContainsKey(cloudUrl);

    public Task<CredentialReadResult> ReadAsync(string cloudUrl, CancellationToken cancellationToken)
    {
        if (Fails)
            throw new CredentialStoreException($"Could not read {DisplayName}.");

        return Task.FromResult(new CredentialReadResult(_records.GetValueOrDefault(cloudUrl), Warning));
    }

    public Task WriteAsync(CredentialRecord record, CancellationToken cancellationToken)
    {
        Add(record);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string cloudUrl, CancellationToken cancellationToken)
    {
        if (Fails)
            throw new CredentialStoreException($"Could not delete from {DisplayName}.");

        Deleted.Add(cloudUrl);
        return Task.FromResult(_records.Remove(cloudUrl));
    }
}
