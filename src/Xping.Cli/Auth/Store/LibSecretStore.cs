/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Cli.Auth.Store;

/// <summary>
/// Keeps sign-ins in the Secret Service (GNOME Keyring, KWallet) through libsecret, one item per
/// Cloud URL (cli-auth-cli-spec §7.3).
/// </summary>
/// <remarks>
/// <para>
/// libsecret is optional on Linux: servers and containers rarely have it, or have it without a
/// session bus to reach the service. It is loaded at run time, so its absence is a reason to fall
/// back to the file, not a crash.
/// </para>
/// <para>
/// The <c>v</c> functions are called, with the attributes in a <c>GHashTable</c>, rather than the
/// variadic ones: variadic arguments do not follow the regular calling convention on every
/// architecture, and .NET cannot call them portably.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
internal sealed class LibSecretStore : IKeychainCredentialStore
{
    /// <summary>
    /// The library that implements the Secret Service client.
    /// </summary>
    public const string LibraryName = "libsecret-1.so.0";

    private readonly string _service;
    private readonly IXpingSerializer _serializer;
    private readonly IEnvironmentVariableProvider _environment;
    private readonly Func<string, bool> _fileExists;
    private readonly Lazy<Native?> _native;

    /// <param name="service">The <c>service</c> attribute: <c>xping-cli</c>, or a test name.</param>
    /// <param name="serializer">Writes and reads the record.</param>
    /// <param name="environment">Where the session bus address is read.</param>
    /// <param name="fileExists">Whether the session bus socket exists.</param>
    /// <param name="libraryName">The library to load; a test names one that does not exist.</param>
    public LibSecretStore(
        string service,
        IXpingSerializer serializer,
        IEnvironmentVariableProvider environment,
        Func<string, bool> fileExists,
        string libraryName = LibraryName)
    {
        _service = service;
        _serializer = serializer;
        _environment = environment;
        _fileExists = fileExists;
        _native = new Lazy<Native?>(() => Native.TryLoad(libraryName));
    }

    /// <inheritdoc/>
    public CredentialStoreKind Kind => CredentialStoreKind.Keychain;

    /// <inheritdoc/>
    public string DisplayName => "Secret Service";

    /// <inheritdoc/>
    public int? MaxSecretBytes => null;

    /// <inheritdoc/>
    public KeychainProbe Probe(string cloudUrl)
    {
        ArgumentNullException.ThrowIfNull(cloudUrl);

        if (_native.Value is null)
            return KeychainProbe.Unavailable("libsecret not installed");

        // Without a session bus libsecret tries to autolaunch one, which fails slowly or, on a
        // server with X11 forwarding, starts a daemon the user did not ask for.
        if (!HasSessionBus())
            return KeychainProbe.Unavailable("Secret Service not running");

        try
        {
            byte[]? secret = Lookup(cloudUrl);
            if (secret is not null)
                Array.Clear(secret);

            return KeychainProbe.Ok;
        }
        catch (CredentialStoreException)
        {
            return KeychainProbe.Unavailable("Secret Service not running");
        }
    }

    /// <inheritdoc/>
    public Task<CredentialReadResult> ReadAsync(string cloudUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cloudUrl);
        cancellationToken.ThrowIfCancellationRequested();

        byte[]? secret = Lookup(cloudUrl);
        if (secret is null)
            return Task.FromResult(CredentialReadResult.None);

        try
        {
            return Task.FromResult(KeychainRecordCodec.Decode(secret, cloudUrl, _serializer));
        }
        finally
        {
            Array.Clear(secret);
        }
    }

    /// <inheritdoc/>
    public unsafe Task WriteAsync(CredentialRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        Native native = Loaded();
        byte[] secret = KeychainRecordCodec.Encode(record, MaxSecretBytes, _serializer, DisplayName);

        // A NUL-terminated copy in native memory, cleared before it is freed.
        byte* password = (byte*)NativeMemory.AllocZeroed((nuint)secret.Length + 1);
        try
        {
            secret.CopyTo(new Span<byte>(password, secret.Length));

            using var label = new Utf8($"{_service} ({record.CloudUrl})");
            using var collection = new Utf8("default");
            using var attributes = new Attributes(native, _service, record.CloudUrl);

            nint error = 0;
            int stored = native.StoreV(native.Schema, attributes.Table, collection.Pointer, label.Pointer, password, 0, &error);
            ThrowOnError(native, error, "write to");

            if (stored == 0)
                throw new CredentialStoreException($"Could not write to the {DisplayName}.");
        }
        finally
        {
            NativeMemory.Clear(password, (nuint)secret.Length);
            NativeMemory.Free(password);
            Array.Clear(secret);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public unsafe Task<bool> DeleteAsync(string cloudUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cloudUrl);
        cancellationToken.ThrowIfCancellationRequested();

        Native native = Loaded();
        using var attributes = new Attributes(native, _service, cloudUrl);

        nint error = 0;
        int cleared = native.ClearV(native.Schema, attributes.Table, 0, &error);
        ThrowOnError(native, error, "delete from");

        return Task.FromResult(cleared != 0);
    }

    private unsafe byte[]? Lookup(string cloudUrl)
    {
        Native native = Loaded();
        using var attributes = new Attributes(native, _service, cloudUrl);

        nint error = 0;
        byte* password = native.LookupV(native.Schema, attributes.Table, 0, &error);
        ThrowOnError(native, error, "read");

        if (password is null)
            return null;

        try
        {
            return MemoryMarshal.CreateReadOnlySpanFromNullTerminated(password).ToArray();
        }
        finally
        {
            // Clears the memory before freeing it.
            native.PasswordFree(password);
        }
    }

    private bool HasSessionBus()
    {
        if (!string.IsNullOrEmpty(_environment.GetVariable("DBUS_SESSION_BUS_ADDRESS")))
            return true;

        string? runtime = _environment.GetVariable("XDG_RUNTIME_DIR");
        return !string.IsNullOrEmpty(runtime) && _fileExists(Path.Combine(runtime, "bus"));
    }

    private Native Loaded() =>
        _native.Value ?? throw new CredentialStoreException($"Could not use the {DisplayName}: libsecret is not installed.");

    private unsafe void ThrowOnError(Native native, nint error, string verb)
    {
        if (error == 0)
            return;

        // GError: { GQuark domain; gint code; gchar *message; }
        var gerror = (GError*)error;
        string message = gerror->Message is null
            ? string.Create(CultureInfo.InvariantCulture, $"error {gerror->Code}")
            : Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(gerror->Message));
        native.ErrorFree(error);

        throw new CredentialStoreException($"Could not {verb} the {DisplayName}: {message}");
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct GError
    {
        public uint Domain;
        public int Code;
        public byte* Message;
    }

    /// <summary>
    /// A UTF-8, NUL-terminated copy of a string in native memory.
    /// </summary>
    private readonly unsafe struct Utf8 : IDisposable
    {
        public Utf8(string value) => Pointer = (byte*)Marshal.StringToCoTaskMemUTF8(value);

        public byte* Pointer { get; }

        public void Dispose() => Marshal.FreeCoTaskMem((nint)Pointer);
    }

    /// <summary>
    /// The <c>service</c> and <c>cloud</c> attributes of one item, as the <c>GHashTable</c> the
    /// <c>v</c> functions take.
    /// </summary>
    /// <remarks>The table does not own its strings; they are freed after it.</remarks>
    private readonly unsafe struct Attributes : IDisposable
    {
        private readonly Native _native;
        private readonly Utf8 _service;
        private readonly Utf8 _cloud;

        public Attributes(Native native, string service, string cloudUrl)
        {
            _native = native;
            _service = new Utf8(service);
            _cloud = new Utf8(cloudUrl);

            Table = native.HashTableNew(native.StrHash, native.StrEqual);
            native.HashTableInsert(Table, (nint)Native.ServiceAttribute, (nint)_service.Pointer);
            native.HashTableInsert(Table, (nint)Native.CloudAttribute, (nint)_cloud.Pointer);
        }

        public nint Table { get; }

        public void Dispose()
        {
            _native.HashTableUnref(Table);
            _service.Dispose();
            _cloud.Dispose();
        }
    }

    /// <summary>
    /// The libsecret and GLib functions, bound at run time, and the item schema.
    /// </summary>
    private sealed unsafe class Native
    {
        private const string GLibName = "libglib-2.0.so.0";

        // SecretSchema: name, flags, 32 × { name, type }, then a reserved int and seven reserved
        // pointers. 64-bit layout; the CLI runs on 64-bit Linux only.
        private const int SchemaSize = 8 + 8 + (32 * 16) + 8 + (7 * 8);

        // Process-lifetime strings the schema and every attribute table point at.
        public static readonly byte* SchemaName = (byte*)Marshal.StringToCoTaskMemUTF8("io.xping.cli");
        public static readonly byte* ServiceAttribute = (byte*)Marshal.StringToCoTaskMemUTF8("service");
        public static readonly byte* CloudAttribute = (byte*)Marshal.StringToCoTaskMemUTF8("cloud");

        private static readonly Lock Gate = new();
        private static readonly Dictionary<string, Native?> Loaded = new(StringComparer.Ordinal);

        public delegate* unmanaged<nint, nint, byte*, byte*, byte*, nint, nint*, int> StoreV;
        public delegate* unmanaged<nint, nint, nint, nint*, byte*> LookupV;
        public delegate* unmanaged<nint, nint, nint, nint*, int> ClearV;
        public delegate* unmanaged<byte*, void> PasswordFree;
        public delegate* unmanaged<nint, nint, nint> HashTableNew;
        public delegate* unmanaged<nint, nint, nint, int> HashTableInsert;
        public delegate* unmanaged<nint, void> HashTableUnref;
        public delegate* unmanaged<nint, void> ErrorFree;
        public nint StrHash;
        public nint StrEqual;
        public nint Schema;

        /// <summary>
        /// Loads <paramref name="libraryName"/> once per process, or returns <see langword="null"/>
        /// when it or a function it needs is missing.
        /// </summary>
        public static Native? TryLoad(string libraryName)
        {
            lock (Gate)
            {
                if (!Loaded.TryGetValue(libraryName, out Native? native))
                {
                    native = Bind(libraryName);
                    Loaded[libraryName] = native;
                }

                return native;
            }
        }

        private static Native? Bind(string libraryName)
        {
            if (!NativeLibrary.TryLoad(libraryName, out nint secret) || !NativeLibrary.TryLoad(GLibName, out nint glib))
                return null;

            if (!TryExport(secret, "secret_password_storev_sync", out nint storeV)
                || !TryExport(secret, "secret_password_lookupv_sync", out nint lookupV)
                || !TryExport(secret, "secret_password_clearv_sync", out nint clearV)
                || !TryExport(secret, "secret_password_free", out nint passwordFree)
                || !TryExport(glib, "g_hash_table_new", out nint hashTableNew)
                || !TryExport(glib, "g_hash_table_insert", out nint hashTableInsert)
                || !TryExport(glib, "g_hash_table_unref", out nint hashTableUnref)
                || !TryExport(glib, "g_error_free", out nint errorFree)
                || !TryExport(glib, "g_str_hash", out nint strHash)
                || !TryExport(glib, "g_str_equal", out nint strEqual))
            {
                return null;
            }

            return new Native
            {
                StoreV = (delegate* unmanaged<nint, nint, byte*, byte*, byte*, nint, nint*, int>)storeV,
                LookupV = (delegate* unmanaged<nint, nint, nint, nint*, byte*>)lookupV,
                ClearV = (delegate* unmanaged<nint, nint, nint, nint*, int>)clearV,
                PasswordFree = (delegate* unmanaged<byte*, void>)passwordFree,
                HashTableNew = (delegate* unmanaged<nint, nint, nint>)hashTableNew,
                HashTableInsert = (delegate* unmanaged<nint, nint, nint, int>)hashTableInsert,
                HashTableUnref = (delegate* unmanaged<nint, void>)hashTableUnref,
                ErrorFree = (delegate* unmanaged<nint, void>)errorFree,
                StrHash = strHash,
                StrEqual = strEqual,
                Schema = CreateSchema(),
            };
        }

        private static bool TryExport(nint library, string name, out nint address) =>
            NativeLibrary.TryGetExport(library, name, out address);

        // Lives as long as the process: libsecret keeps no reference, but every call takes it.
        private static nint CreateSchema()
        {
            byte* schema = (byte*)NativeMemory.AllocZeroed(SchemaSize);

            *(byte**)schema = SchemaName;                       // name; flags stay SECRET_SCHEMA_NONE
            *(byte**)(schema + 16) = ServiceAttribute;          // attributes[0], type STRING (0)
            *(byte**)(schema + 32) = CloudAttribute;            // attributes[1], type STRING (0)

            return (nint)schema;
        }
    }
}
