/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth.Store;

/// <summary>
/// A usable stored sign-in and the store it was read from.
/// </summary>
/// <param name="Record">The record.</param>
/// <param name="Store">Where it is kept; <c>auth status</c> names it.</param>
internal sealed record StoredLogin(CredentialRecord Record, ICredentialStore Store);

/// <summary>
/// What a lookup across every store found.
/// </summary>
/// <param name="Login">The first usable sign-in in read order, or <see langword="null"/>.</param>
/// <param name="Warnings">Why entries that exist could not be used, worded for the user.</param>
/// <param name="Failures">Why stores could not be read at all, worded for the user.</param>
internal sealed record StoredLoginLookup(StoredLogin? Login, IReadOnlyList<string> Warnings, IReadOnlyList<string> Failures);

/// <summary>
/// The credential stores of this process: the one sign-ins are written to, and the order they are
/// read in (cli-auth-cli-spec §7.4).
/// </summary>
/// <remarks>
/// Reads go keychain first, then file, whichever was selected for writes: a sign-in made over SSH
/// (file) must still work in a desktop session where the keychain is available. Commands use this
/// class, never a backend, so the rule holds everywhere.
/// </remarks>
internal sealed class CredentialStores
{
    public CredentialStores(ICredentialStore selected, IReadOnlyList<ICredentialStore> readOrder, string? fallbackReason)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(readOrder);

        if (!readOrder.Contains(selected))
            throw new ArgumentException("The selected store must be in the read order.", nameof(readOrder));

        Selected = selected;
        ReadOrder = readOrder;
        FallbackReason = fallbackReason;
    }

    /// <summary>
    /// Gets the store new sign-ins are written to.
    /// </summary>
    public ICredentialStore Selected { get; }

    /// <summary>
    /// Gets every store, in the order they are read.
    /// </summary>
    public IReadOnlyList<ICredentialStore> ReadOrder { get; }

    /// <summary>
    /// Gets why the file store was selected instead of a keychain, worded for the user
    /// ("Secret Service not running"), or <see langword="null"/> when a keychain was selected.
    /// </summary>
    public string? FallbackReason { get; }

    /// <summary>
    /// Returns the first usable sign-in for <paramref name="cloudUrl"/>, in read order.
    /// </summary>
    /// <remarks>
    /// A store that cannot be read is recorded in <see cref="StoredLoginLookup.Failures"/> and the
    /// next one is tried: a locked keychain must not hide a sign-in kept in the file.
    /// </remarks>
    public async Task<StoredLoginLookup> ReadAsync(string cloudUrl, CancellationToken cancellationToken)
    {
        List<string> warnings = [];
        List<string> failures = [];

        foreach (ICredentialStore store in ReadOrder)
        {
            CredentialReadResult result;
            try
            {
                result = await store.ReadAsync(cloudUrl, cancellationToken).ConfigureAwait(false);
            }
            catch (CredentialStoreException ex)
            {
                failures.Add(ex.Message);
                continue;
            }

            if (result.Warning is not null)
                warnings.Add(result.Warning);

            if (result.Record is not null)
                return new StoredLoginLookup(new StoredLogin(result.Record, store), warnings, failures);
        }

        return new StoredLoginLookup(null, warnings, failures);
    }

    /// <summary>
    /// Stores <paramref name="record"/> in <see cref="Selected"/>.
    /// </summary>
    /// <remarks>
    /// When that is a keychain, a file entry for the same Cloud URL is removed, so the two never
    /// disagree about who is signed in.
    /// </remarks>
    /// <exception cref="CredentialStoreException">A store could not be written.</exception>
    public async Task WriteAsync(CredentialRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        await Selected.WriteAsync(record, cancellationToken).ConfigureAwait(false);

        if (Selected.Kind == CredentialStoreKind.File)
            return;

        foreach (ICredentialStore store in ReadOrder.Where(s => s.Kind == CredentialStoreKind.File))
            await store.DeleteAsync(record.CloudUrl, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the sign-in for <paramref name="cloudUrl"/> from every store.
    /// </summary>
    /// <remarks>
    /// Every store is tried even after one fails, so signing out removes whatever it can.
    /// </remarks>
    /// <returns>Whether any store had an entry.</returns>
    /// <exception cref="CredentialStoreException">
    /// One or more stores could not be changed; the others were.
    /// </exception>
    public async Task<bool> DeleteAllAsync(string cloudUrl, CancellationToken cancellationToken)
    {
        bool deleted = false;
        List<CredentialStoreException> failures = [];

        foreach (ICredentialStore store in ReadOrder)
        {
            try
            {
                deleted |= await store.DeleteAsync(cloudUrl, cancellationToken).ConfigureAwait(false);
            }
            catch (CredentialStoreException ex)
            {
                failures.Add(ex);
            }
        }

        return failures switch
        {
            [] => deleted,
            [var only] => throw only,
            _ => throw new CredentialStoreException(
                string.Join(" ", failures.Select(f => f.Message)),
                new AggregateException(failures)),
        };
    }
}
