/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Cli.Auth.Store;

/// <summary>
/// Keeps sign-ins in the Windows Credential Manager, one generic credential per Cloud URL
/// (cli-auth-cli-spec §7.3).
/// </summary>
/// <remarks>
/// The entry is <c>{service}:{cloudUrl}</c>, persisted for the local machine so it survives sign-out
/// but does not roam with the profile. <c>CredWrite</c> replaces an entry whole, so a reader never
/// sees half of one.
/// </remarks>
/// <param name="service">The entry prefix: <c>xping-cli</c>, or a test name.</param>
/// <param name="serializer">Writes and reads the record.</param>
[SupportedOSPlatform("windows")]
internal sealed partial class WindowsCredentialStore(string service, IXpingSerializer serializer) : IKeychainCredentialStore
{
    /// <summary>
    /// <c>CRED_MAX_CREDENTIAL_BLOB_SIZE</c>: the most a generic credential holds.
    /// </summary>
    public const int BlobLimit = 5 * 512;

    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    /// <inheritdoc/>
    public CredentialStoreKind Kind => CredentialStoreKind.Keychain;

    /// <inheritdoc/>
    public string DisplayName => "Windows Credential Manager";

    /// <inheritdoc/>
    public int? MaxSecretBytes => BlobLimit;

    /// <inheritdoc/>
    public KeychainProbe Probe(string cloudUrl)
    {
        ArgumentNullException.ThrowIfNull(cloudUrl);

        if (NativeMethods.CredRead(Target(cloudUrl), CredTypeGeneric, 0, out nint credential))
        {
            NativeMethods.CredFree(credential);
            return KeychainProbe.Ok;
        }

        return Marshal.GetLastPInvokeError() == ErrorNotFound
            ? KeychainProbe.Ok
            : KeychainProbe.Unavailable("Credential Manager error");
    }

    /// <inheritdoc/>
    public Task<CredentialReadResult> ReadAsync(string cloudUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cloudUrl);
        cancellationToken.ThrowIfCancellationRequested();

        if (!NativeMethods.CredRead(Target(cloudUrl), CredTypeGeneric, 0, out nint credential))
        {
            int error = Marshal.GetLastPInvokeError();
            return error == ErrorNotFound
                ? Task.FromResult(CredentialReadResult.None)
                : throw Failure("read", error);
        }

        byte[] secret = [];
        try
        {
            unsafe
            {
                var native = (NativeCredential*)credential;
                secret = new ReadOnlySpan<byte>(native->CredentialBlob, (int)native->CredentialBlobSize).ToArray();
            }

            return Task.FromResult(KeychainRecordCodec.Decode(secret, cloudUrl, serializer));
        }
        finally
        {
            Array.Clear(secret);
            NativeMethods.CredFree(credential);
        }
    }

    /// <inheritdoc/>
    public Task WriteAsync(CredentialRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        byte[] secret = KeychainRecordCodec.Encode(record, MaxSecretBytes, serializer, DisplayName);
        try
        {
            unsafe
            {
                fixed (char* target = Target(record.CloudUrl))
                fixed (char* user = service)
                fixed (byte* blob = secret)
                {
                    var credential = new NativeCredential
                    {
                        Type = CredTypeGeneric,
                        TargetName = target,
                        CredentialBlobSize = (uint)secret.Length,
                        CredentialBlob = blob,
                        Persist = CredPersistLocalMachine,
                        UserName = user,
                    };

                    if (!NativeMethods.CredWrite(&credential, 0))
                        throw Failure("write to", Marshal.GetLastPInvokeError());
                }
            }
        }
        finally
        {
            Array.Clear(secret);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string cloudUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cloudUrl);
        cancellationToken.ThrowIfCancellationRequested();

        if (NativeMethods.CredDelete(Target(cloudUrl), CredTypeGeneric, 0))
            return Task.FromResult(true);

        int error = Marshal.GetLastPInvokeError();
        return error == ErrorNotFound ? Task.FromResult(false) : throw Failure("delete from", error);
    }

    private string Target(string cloudUrl) => $"{service}:{cloudUrl}";

    private CredentialStoreException Failure(string verb, int error) =>
        new(string.Create(CultureInfo.InvariantCulture, $"Could not {verb} {DisplayName} (error {error})."));

    // CREDENTIALW. Only the members the store sets or reads are named; the rest stay zero.
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public char* TargetName;
        public char* Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public byte* CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public nint Attributes;
        public char* TargetAlias;
        public char* UserName;
    }

    private static unsafe partial class NativeMethods
    {
        [LibraryImport("advapi32.dll", EntryPoint = "CredReadW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CredRead(string targetName, uint type, uint flags, out nint credential);

        [LibraryImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CredWrite(NativeCredential* credential, uint flags);

        [LibraryImport("advapi32.dll", EntryPoint = "CredDeleteW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CredDelete(string targetName, uint type, uint flags);

        [LibraryImport("advapi32.dll", EntryPoint = "CredFree")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static partial void CredFree(nint buffer);
    }
}
