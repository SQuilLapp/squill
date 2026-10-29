using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Squill.Platform.Secrets;

/// <summary>macOS <see cref="ISecretStore"/> backed by Keychain generic passwords of the current user.</summary>
[SupportedOSPlatform("macos")]
public sealed class KeychainSecretStore : ISecretStore
{
    private const int Success = 0;
    private const int ItemNotFound = -25300;
    private const int DuplicateItem = -25299;
    private static readonly byte[] _service = Encoding.UTF8.GetBytes("squill");

    /// <inheritdoc/>
    public ValueTask SetAsync(string key, string secret, CancellationToken ct) =>
        new(Task.Run(() => Set(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(secret)), ct));

    /// <inheritdoc/>
    public ValueTask<string?> GetAsync(string key, CancellationToken ct) =>
        new(Task.Run(() => Get(Encoding.UTF8.GetBytes(key)), ct));

    /// <inheritdoc/>
    public ValueTask DeleteAsync(string key, CancellationToken ct) =>
        new(Task.Run(() => Delete(Encoding.UTF8.GetBytes(key)), ct));

    private static void Set(byte[] account, byte[] password)
    {
        int status = KeychainNative.AddGenericPassword(IntPtr.Zero, (uint)_service.Length, _service, (uint)account.Length, account, (uint)password.Length, password, IntPtr.Zero);
        if (status != DuplicateItem)
        {
            Check(status);
            return;
        }

        IntPtr item = Find(account, out _, out IntPtr data);
        try
        {
            Check(KeychainNative.ModifyData(item, IntPtr.Zero, (uint)password.Length, password));
        }
        finally
        {
            Free(item, data);
        }
    }

    private static string? Get(byte[] account)
    {
        IntPtr item = Find(account, out uint length, out IntPtr data);
        if (item == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUTF8(data, (int)length);
        }
        finally
        {
            Free(item, data);
        }
    }

    private static void Delete(byte[] account)
    {
        IntPtr item = Find(account, out _, out IntPtr data);
        if (item == IntPtr.Zero)
        {
            return;
        }

        try
        {
            Check(KeychainNative.ItemDelete(item));
        }
        finally
        {
            Free(item, data);
        }
    }

    // Returns the item reference (zero when absent); the caller releases it and the data through Free.
    private static IntPtr Find(byte[] account, out uint length, out IntPtr data)
    {
        int status = KeychainNative.FindGenericPassword(IntPtr.Zero, (uint)_service.Length, _service, (uint)account.Length, account, out length, out data, out IntPtr item);
        if (status == ItemNotFound)
        {
            return IntPtr.Zero;
        }

        Check(status);
        return item;
    }

    private static void Free(IntPtr item, IntPtr data)
    {
        if (data != IntPtr.Zero)
        {
            KeychainNative.FreeContent(IntPtr.Zero, data);
        }

        if (item != IntPtr.Zero)
        {
            KeychainNative.Release(item);
        }
    }

    private static void Check(int status)
    {
        if (status != Success)
        {
            throw new InvalidOperationException($"Keychain call failed with status {status}.");
        }
    }
}
