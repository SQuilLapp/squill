using System.Runtime.InteropServices;

namespace Squill.Platform.Secrets;

/// <summary>Source-generated bindings to libsecret and the GLib functions needed to call it.</summary>
internal static partial class LibSecretNative
{
    private const string Secret = "libsecret-1.so.0";
    private const string GLib = "libglib-2.0.so.0";

    /// <summary>Calls <c>secret_password_storev_sync</c>.</summary>
    [LibraryImport(Secret, EntryPoint = "secret_password_storev_sync", StringMarshalling = StringMarshalling.Utf8)]
    public static partial void StoreSync(IntPtr schema, IntPtr attributes, string collection, string label, string password, IntPtr cancellable, out IntPtr error);

    /// <summary>Calls <c>secret_password_lookupv_sync</c>.</summary>
    /// <returns>A newly allocated UTF-8 string to release with <see cref="PasswordFree"/>, or zero when absent.</returns>
    [LibraryImport(Secret, EntryPoint = "secret_password_lookupv_sync")]
    public static partial IntPtr LookupSync(IntPtr schema, IntPtr attributes, IntPtr cancellable, out IntPtr error);

    /// <summary>Calls <c>secret_password_clearv_sync</c>.</summary>
    [LibraryImport(Secret, EntryPoint = "secret_password_clearv_sync")]
    public static partial void ClearSync(IntPtr schema, IntPtr attributes, IntPtr cancellable, out IntPtr error);

    /// <summary>Calls <c>secret_password_free</c>.</summary>
    [LibraryImport(Secret, EntryPoint = "secret_password_free")]
    public static partial void PasswordFree(IntPtr password);

    /// <summary>Calls <c>g_hash_table_new</c>.</summary>
    [LibraryImport(GLib, EntryPoint = "g_hash_table_new")]
    public static partial IntPtr HashTableNew(IntPtr hashFunc, IntPtr keyEqualFunc);

    /// <summary>Calls <c>g_hash_table_insert</c>.</summary>
    [LibraryImport(GLib, EntryPoint = "g_hash_table_insert")]
    public static partial void HashTableInsert(IntPtr table, IntPtr key, IntPtr value);

    /// <summary>Calls <c>g_hash_table_unref</c>.</summary>
    [LibraryImport(GLib, EntryPoint = "g_hash_table_unref")]
    public static partial void HashTableUnref(IntPtr table);

    /// <summary>Calls <c>g_error_free</c>.</summary>
    [LibraryImport(GLib, EntryPoint = "g_error_free")]
    public static partial void ErrorFree(IntPtr error);

    /// <summary>Resolves a GLib export (such as <c>g_str_hash</c>) as a function pointer.</summary>
    /// <param name="name">Exported symbol name.</param>
    /// <returns>The symbol address.</returns>
    public static IntPtr GLibExport(string name) =>
        NativeLibrary.GetExport(NativeLibrary.Load(GLib), name);
}
