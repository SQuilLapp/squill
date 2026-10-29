# P2-03 - Platform: macOS Keychain store and secret store factory

Status: done
Depends on: P2-02

## Goal
Complete cross-platform secret storage (risk R6, part 3).

## Design decisions (final)
- `[SupportedOSPlatform("macos")] KeychainSecretStore : ISecretStore` with `LibraryImport` on `/System/Library/Frameworks/Security.framework/Security`: `SecKeychainAddGenericPassword`, `SecKeychainFindGenericPassword`, `SecKeychainItemDelete`, `SecKeychainItemFreeContent` (deprecated but blittable); service name `squill`, account name = key; `CFRelease` through `CoreFoundation.framework`. `errSecDuplicateItem` on set => find, then `SecKeychainItemModifyAttributesAndData`. Status codes other than success/`errSecItemNotFound` throw `InvalidOperationException` with the code.
- `static class SecretStoreFactory { static ISecretStore Create(string dataDirectory) }` in `Squill.Platform.Secrets`: Windows => `DpapiSecretStore(Path.Combine(dataDirectory, "secrets"))`, macOS => Keychain, Linux => libsecret, else `PlatformNotSupportedException`.
- Same STOP rule as P2-02 for unsafe marshalling problems.

## Out of scope
- Verification on a real Keychain.
- Fallback encrypted-file store for headless Linux.

## Tasks
- [x] Implement the Keychain store and the factory.
- [x] Tests: factory returns `DpapiSecretStore` on Windows (skipped elsewhere) and never throws on the current OS.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- The card's report says whether the Keychain path was executed.

## Suggested commit message
Add macOS Keychain secret store and factory
