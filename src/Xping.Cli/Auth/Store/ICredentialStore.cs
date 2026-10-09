/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth.Store;

/// <summary>
/// The kind of place a credential store keeps its records.
/// </summary>
internal enum CredentialStoreKind
{
    /// <summary>The operating system's credential store.</summary>
    Keychain,

    /// <summary><c>~/.xping/credentials.json</c>.</summary>
    File
}

/// <summary>
/// What a store found for one Cloud URL.
/// </summary>
/// <param name="Record">The usable record, or <see langword="null"/>.</param>
/// <param name="Warning">
/// Why an entry that exists could not be used (corrupt, or a file others can read), worded for the
/// user; <see langword="null"/> when there is nothing to say.
/// </param>
internal sealed record CredentialReadResult(CredentialRecord? Record, string? Warning)
{
    /// <summary>
    /// Nothing is stored and nothing is wrong.
    /// </summary>
    public static CredentialReadResult None { get; } = new(null, null);

    /// <summary>
    /// An entry exists for <paramref name="cloudUrl"/> but cannot be used (cli-auth-cli-spec §7.7).
    /// </summary>
    /// <remarks>Nothing is deleted: a newer CLI may have written it, and only a login replaces it.</remarks>
    public static CredentialReadResult Corrupt(string cloudUrl) =>
        new(null, $"Stored credentials for {cloudUrl} are unreadable and will be replaced at the next `xping login`.");
}

/// <summary>
/// One place sign-ins are kept, keyed by normalized Cloud URL (cli-auth-cli-spec §7.3).
/// </summary>
internal interface ICredentialStore
{
    /// <summary>
    /// Gets the kind of store.
    /// </summary>
    CredentialStoreKind Kind { get; }

    /// <summary>
    /// Gets the name shown to the user: "macOS Keychain", "~/.xping/credentials.json", ….
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Reads the record for <paramref name="cloudUrl"/>.
    /// </summary>
    /// <exception cref="CredentialStoreException">The store could not be read at all.</exception>
    Task<CredentialReadResult> ReadAsync(string cloudUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Creates or replaces the record for its Cloud URL, atomically.
    /// </summary>
    /// <exception cref="CredentialStoreException">The record could not be stored.</exception>
    Task WriteAsync(CredentialRecord record, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the record for <paramref name="cloudUrl"/>.
    /// </summary>
    /// <returns>Whether there was an entry to remove.</returns>
    /// <exception cref="CredentialStoreException">The entry could not be removed.</exception>
    Task<bool> DeleteAsync(string cloudUrl, CancellationToken cancellationToken);
}

/// <summary>
/// A credential store could not be read or written.
/// </summary>
/// <remarks>
/// The message names the store or the path, never its content. Commands that need the store fail
/// with <see cref="AuthExitCodes.CredentialStoreError"/>; <c>report</c> degrades to local.
/// </remarks>
internal sealed class CredentialStoreException : Exception
{
    public CredentialStoreException()
    {
    }

    public CredentialStoreException(string message)
        : base(message)
    {
    }

    public CredentialStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
