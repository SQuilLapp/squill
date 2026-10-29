using System.Runtime.InteropServices;

namespace Squill.Platform.Secrets;

/// <summary>Source-generated bindings to the macOS Security and CoreFoundation frameworks (legacy generic-password API).</summary>
internal static partial class KeychainNative
{
    private const string Security = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    /// <summary>Calls <c>SecKeychainAddGenericPassword</c> on the default keychain.</summary>
    /// <returns>The <c>OSStatus</c> result.</returns>
    [LibraryImport(Security, EntryPoint = "SecKeychainAddGenericPassword")]
    public static partial int AddGenericPassword(IntPtr keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account, uint passwordLength, byte[] password, IntPtr itemRef);

    /// <summary>Calls <c>SecKeychainFindGenericPassword</c> on the default keychains.</summary>
    /// <returns>The <c>OSStatus</c> result.</returns>
    [LibraryImport(Security, EntryPoint = "SecKeychainFindGenericPassword")]
    public static partial int FindGenericPassword(IntPtr keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account, out uint passwordLength, out IntPtr password, out IntPtr itemRef);

    /// <summary>Calls <c>SecKeychainItemModifyAttributesAndData</c> to replace only the password data.</summary>
    /// <returns>The <c>OSStatus</c> result.</returns>
    [LibraryImport(Security, EntryPoint = "SecKeychainItemModifyAttributesAndData")]
    public static partial int ModifyData(IntPtr itemRef, IntPtr attributes, uint length, byte[] data);

    /// <summary>Calls <c>SecKeychainItemDelete</c>.</summary>
    /// <returns>The <c>OSStatus</c> result.</returns>
    [LibraryImport(Security, EntryPoint = "SecKeychainItemDelete")]
    public static partial int ItemDelete(IntPtr itemRef);

    /// <summary>Calls <c>SecKeychainItemFreeContent</c> to release password data returned by a find; the status is ignored.</summary>
    [LibraryImport(Security, EntryPoint = "SecKeychainItemFreeContent")]
    public static partial void FreeContent(IntPtr attributes, IntPtr data);

    /// <summary>Calls <c>CFRelease</c>.</summary>
    [LibraryImport(CoreFoundation, EntryPoint = "CFRelease")]
    public static partial void Release(IntPtr reference);
}
