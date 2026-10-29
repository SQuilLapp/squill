using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Squill.Platform.Secrets;

/// <summary>Linux <see cref="ISecretStore"/> backed by the freedesktop Secret Service through libsecret.</summary>
[SupportedOSPlatform("linux")]
public sealed class LibSecretStore : ISecretStore
{
    private const int SchemaSize = 1024;
    private const int SchemaFlagsOffset = 8;
    private const int SchemaAttributesOffset = 16;
    private const int AttributeSize = 16;
    private const int StringAttribute = 0;
    private const int GErrorMessageOffset = 8;

    private static readonly Lazy<IntPtr> _schema = new(BuildSchema);

    /// <inheritdoc/>
    public ValueTask SetAsync(string key, string secret, CancellationToken ct) =>
        new(Task.Run(() => WithAttributes(key, table =>
        {
            LibSecretNative.StoreSync(_schema.Value, table, "default", "SQuilL secret", secret, IntPtr.Zero, out IntPtr error);
            ThrowIfError(error);
            return 0;
        }), ct));

    /// <inheritdoc/>
    public ValueTask<string?> GetAsync(string key, CancellationToken ct) =>
        new(Task.Run(() => WithAttributes(key, table =>
        {
            IntPtr found = LibSecretNative.LookupSync(_schema.Value, table, IntPtr.Zero, out IntPtr error);
            ThrowIfError(error);
            if (found == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUTF8(found);
            }
            finally
            {
                LibSecretNative.PasswordFree(found);
            }
        }), ct));

    /// <inheritdoc/>
    public ValueTask DeleteAsync(string key, CancellationToken ct) =>
        new(Task.Run(() => WithAttributes(key, table =>
        {
            LibSecretNative.ClearSync(_schema.Value, table, IntPtr.Zero, out IntPtr error);
            ThrowIfError(error);
            return 0;
        }), ct));

    private static T WithAttributes<T>(string key, Func<IntPtr, T> action)
    {
        IntPtr table = LibSecretNative.HashTableNew(LibSecretNative.GLibExport("g_str_hash"), LibSecretNative.GLibExport("g_str_equal"));
        var allocated = new List<IntPtr>();
        try
        {
            foreach ((string name, string value) in LibSecretAttributes.For(key))
            {
                IntPtr n = Marshal.StringToCoTaskMemUTF8(name);
                IntPtr v = Marshal.StringToCoTaskMemUTF8(value);
                allocated.Add(n);
                allocated.Add(v);
                LibSecretNative.HashTableInsert(table, n, v);
            }

            return action(table);
        }
        finally
        {
            LibSecretNative.HashTableUnref(table);
            foreach (IntPtr p in allocated)
            {
                Marshal.FreeCoTaskMem(p);
            }
        }
    }

    // SecretSchema: name, flags (SECRET_SCHEMA_NONE = 0), 32 {name, type} attributes; zeroed tail covers the reserved fields.
    private static IntPtr BuildSchema()
    {
        IntPtr schema = Marshal.AllocHGlobal(SchemaSize);
        for (int i = 0; i < SchemaSize; i += sizeof(long))
        {
            Marshal.WriteInt64(schema, i, 0);
        }

        Marshal.WriteIntPtr(schema, 0, Marshal.StringToCoTaskMemUTF8("org.squill.Secret"));
        Marshal.WriteInt32(schema, SchemaFlagsOffset, 0);
        string[] names = ["application", "key"];
        for (int i = 0; i < names.Length; i++)
        {
            int offset = SchemaAttributesOffset + (i * AttributeSize);
            Marshal.WriteIntPtr(schema, offset, Marshal.StringToCoTaskMemUTF8(names[i]));
            Marshal.WriteInt32(schema, offset + IntPtr.Size, StringAttribute);
        }

        return schema;
    }

    private static void ThrowIfError(IntPtr error)
    {
        if (error == IntPtr.Zero)
        {
            return;
        }

        string? message = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(error, GErrorMessageOffset));
        LibSecretNative.ErrorFree(error);
        throw new InvalidOperationException(message ?? "Secret Service call failed.");
    }
}
