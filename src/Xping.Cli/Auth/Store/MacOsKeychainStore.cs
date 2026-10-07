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
/// Keeps sign-ins in the user's login keychain, one generic password per Cloud URL
/// (cli-auth-cli-spec §7.3).
/// </summary>
/// <remarks>
/// The item is service <c>{service}</c>, account <c>{cloudUrl}</c>. The tool is not signed, so the
/// keychain asks once after every upgrade whether <c>xping</c> may read the item; that is
/// documented, not worked around.
/// </remarks>
/// <param name="service">The item's service: <c>xping-cli</c>, or a test name.</param>
/// <param name="serializer">Writes and reads the record.</param>
[SupportedOSPlatform("macos")]
internal sealed partial class MacOsKeychainStore(string service, IXpingSerializer serializer) : IKeychainCredentialStore
{
    private const int ErrSecSuccess = 0;
    private const int ErrSecItemNotFound = -25300;
    private const int ErrSecInteractionNotAllowed = -25308;
    private const int ErrSecAuthFailed = -25293;

    private readonly ProbedSecret _probed = new();

    /// <inheritdoc/>
    public CredentialStoreKind Kind => CredentialStoreKind.Keychain;

    /// <inheritdoc/>
    public string DisplayName => "macOS Keychain";

    /// <inheritdoc/>
    public int? MaxSecretBytes => null;

    /// <inheritdoc/>
    public KeychainProbe Probe(string cloudUrl)
    {
        ArgumentNullException.ThrowIfNull(cloudUrl);

        int status;
        try
        {
            // The data is asked for, not only the item: a locked keychain still lists items over SSH
            // but refuses their content, and the content is what a sign-in needs.
            status = CopyData(cloudUrl, out byte[]? secret);
            if (status is ErrSecSuccess or ErrSecItemNotFound)
                _probed.Keep(cloudUrl, secret);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or TypeInitializationException
            or CredentialStoreException)
        {
            return KeychainProbe.Unavailable("keychain unavailable");
        }

        return status switch
        {
            ErrSecSuccess or ErrSecItemNotFound => KeychainProbe.Ok,
            ErrSecInteractionNotAllowed or ErrSecAuthFailed => KeychainProbe.Unavailable("keychain locked"),
            _ => KeychainProbe.Unavailable("keychain unavailable"),
        };
    }

    /// <inheritdoc/>
    public Task<CredentialReadResult> ReadAsync(string cloudUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cloudUrl);
        cancellationToken.ThrowIfCancellationRequested();

        int status = _probed.TryTake(cloudUrl, out byte[]? secret)
            ? secret is null ? ErrSecItemNotFound : ErrSecSuccess
            : CopyData(cloudUrl, out secret);
        try
        {
            return status switch
            {
                ErrSecSuccess => Task.FromResult(CredentialRecordCodec.Decode(secret, cloudUrl, serializer)),
                ErrSecItemNotFound => Task.FromResult(CredentialReadResult.None),
                _ => throw Failure("read", status),
            };
        }
        finally
        {
            if (secret is not null)
                Array.Clear(secret);
        }
    }

    /// <inheritdoc/>
    public Task WriteAsync(CredentialRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        _probed.Forget();
        byte[] secret = CredentialRecordCodec.Encode(record, MaxSecretBytes, serializer, DisplayName);
        try
        {
            using var scope = new CoreFoundation.Scope();
            nint data = scope.Data(secret);

            // Update in place first: deleting and adding would leave a moment with no sign-in, and
            // would reset the "Always Allow" the user gave the existing item.
            nint query = ItemQuery(scope, record.CloudUrl, returnData: false);
            nint changes = scope.Dictionary([(Security.ValueData, data)]);
            int status = Security.SecItemUpdate(query, changes);

            if (status == ErrSecItemNotFound)
            {
                nint item = scope.Dictionary(
                [
                    (Security.Class, Security.ClassGenericPassword),
                    (Security.AttrService, scope.String(service)),
                    (Security.AttrAccount, scope.String(record.CloudUrl)),
                    (Security.ValueData, data),
                ]);
                status = Security.SecItemAdd(item, 0);
            }

            if (status != ErrSecSuccess)
                throw Failure("write to", status);
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

        _probed.Forget();
        using var scope = new CoreFoundation.Scope();
        int status = Security.SecItemDelete(ItemQuery(scope, cloudUrl, returnData: false));

        return status switch
        {
            ErrSecSuccess => Task.FromResult(true),
            ErrSecItemNotFound => Task.FromResult(false),
            _ => throw Failure("delete from", status),
        };
    }

    private int CopyData(string cloudUrl, out byte[]? secret)
    {
        secret = null;

        using var scope = new CoreFoundation.Scope();
        int status = Security.SecItemCopyMatching(ItemQuery(scope, cloudUrl, returnData: true), out nint data);
        if (status != ErrSecSuccess)
            return status;

        scope.Own(data);
        secret = CoreFoundation.Bytes(data);
        return status;
    }

    private nint ItemQuery(CoreFoundation.Scope scope, string cloudUrl, bool returnData)
    {
        (nint Key, nint Value) itemClass = (Security.Class, Security.ClassGenericPassword);
        (nint Key, nint Value) itemService = (Security.AttrService, scope.String(service));
        (nint Key, nint Value) itemAccount = (Security.AttrAccount, scope.String(cloudUrl));

        return returnData
            ? scope.Dictionary(
            [
                itemClass,
                itemService,
                itemAccount,
                (Security.ReturnData, CoreFoundation.True),
                (Security.MatchLimit, Security.MatchLimitOne),
            ])
            : scope.Dictionary([itemClass, itemService, itemAccount]);
    }

    private CredentialStoreException Failure(string verb, int status) =>
        new(string.Create(CultureInfo.InvariantCulture, $"Could not {verb} the {DisplayName} (OSStatus {status})."));

    /// <summary>
    /// Security.framework: the item functions and the constants that key their dictionaries.
    /// </summary>
    private static partial class Security
    {
        private const string Library = "/System/Library/Frameworks/Security.framework/Security";

        private static readonly nint Handle = NativeLibrary.Load(Library);

        public static readonly nint Class = Constant("kSecClass");
        public static readonly nint ClassGenericPassword = Constant("kSecClassGenericPassword");
        public static readonly nint AttrService = Constant("kSecAttrService");
        public static readonly nint AttrAccount = Constant("kSecAttrAccount");
        public static readonly nint ValueData = Constant("kSecValueData");
        public static readonly nint ReturnData = Constant("kSecReturnData");
        public static readonly nint MatchLimit = Constant("kSecMatchLimit");
        public static readonly nint MatchLimitOne = Constant("kSecMatchLimitOne");

        [LibraryImport(Library)]
        public static partial int SecItemCopyMatching(nint query, out nint result);

        [LibraryImport(Library)]
        public static partial int SecItemAdd(nint attributes, nint result);

        [LibraryImport(Library)]
        public static partial int SecItemUpdate(nint query, nint attributesToUpdate);

        [LibraryImport(Library)]
        public static partial int SecItemDelete(nint query);

        // The exported symbol is the address of a CFStringRef variable, not the string itself.
        private static nint Constant(string name) => Marshal.ReadIntPtr(NativeLibrary.GetExport(Handle, name));
    }

    /// <summary>
    /// CoreFoundation: the strings, data and dictionaries the item functions take.
    /// </summary>
    private static partial class CoreFoundation
    {
        private const string Library = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const uint StringEncodingUtf8 = 0x08000100;

        private static readonly nint Handle = NativeLibrary.Load(Library);

        // The callbacks are passed by address; True is the address of a CFBooleanRef variable.
        private static readonly nint KeyCallBacks = NativeLibrary.GetExport(Handle, "kCFTypeDictionaryKeyCallBacks");
        private static readonly nint ValueCallBacks = NativeLibrary.GetExport(Handle, "kCFTypeDictionaryValueCallBacks");

        public static readonly nint True = Marshal.ReadIntPtr(NativeLibrary.GetExport(Handle, "kCFBooleanTrue"));

        public static unsafe byte[] Bytes(nint data) =>
            new ReadOnlySpan<byte>(CFDataGetBytePtr(data), checked((int)CFDataGetLength(data))).ToArray();

        [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
        private static partial nint CFStringCreateWithCString(nint allocator, string value, uint encoding);

        [LibraryImport(Library)]
        private static unsafe partial nint CFDataCreate(nint allocator, byte* bytes, nint length);

        [LibraryImport(Library)]
        private static unsafe partial nint CFDictionaryCreate(
            nint allocator, nint* keys, nint* values, nint count, nint keyCallBacks, nint valueCallBacks);

        [LibraryImport(Library)]
        private static partial nint CFDataGetLength(nint data);

        [LibraryImport(Library)]
        private static unsafe partial byte* CFDataGetBytePtr(nint data);

        [LibraryImport(Library)]
        private static partial void CFRelease(nint value);

        /// <summary>
        /// Owns the CoreFoundation objects of one call and releases them together.
        /// </summary>
        /// <remarks>
        /// A dictionary retains its keys and values, so releasing everything at the end, in any
        /// order, is correct.
        /// </remarks>
        internal sealed class Scope : IDisposable
        {
            private readonly List<nint> _owned = [];

            public nint Own(nint value)
            {
                if (value == 0)
                    throw new CredentialStoreException("Could not use the macOS Keychain (CoreFoundation allocation failed).");

                _owned.Add(value);
                return value;
            }

            public nint String(string value) => Own(CFStringCreateWithCString(0, value, StringEncodingUtf8));

            public unsafe nint Data(byte[] value)
            {
                fixed (byte* bytes = value)
                    return Own(CFDataCreate(0, bytes, value.Length));
            }

            public unsafe nint Dictionary(ReadOnlySpan<(nint Key, nint Value)> entries)
            {
                nint* keys = stackalloc nint[entries.Length];
                nint* values = stackalloc nint[entries.Length];

                for (int i = 0; i < entries.Length; i++)
                {
                    keys[i] = entries[i].Key;
                    values[i] = entries[i].Value;
                }

                return Own(CFDictionaryCreate(0, keys, values, entries.Length, KeyCallBacks, ValueCallBacks));
            }

            public void Dispose()
            {
                foreach (nint value in _owned)
                    CFRelease(value);

                _owned.Clear();
            }
        }
    }
}
