# P2-02 - Platform: libsecret secret store (Linux)

Status: done
Depends on: P2-01

## Goal
Store secrets in the freedesktop Secret Service through source-generated P/Invoke (risk R6, part 2).

## Design decisions (final)
- `[SupportedOSPlatform("linux")] LibSecretStore : ISecretStore` in `Squill.Platform.Secrets`, with an internal `static partial class LibSecretNative` using `[LibraryImport("libsecret-1.so.0")]` (never `DllImport`, never runtime marshalling).
- Use the non-variadic `secret_password_storev_sync`, `secret_password_lookupv_sync`, `secret_password_clearv_sync` with a `GHashTable*` of attributes `{ "application": "squill", "key": <key> }` built through `LibraryImport` calls to `libglib-2.0.so.0` (`g_hash_table_new`, `g_hash_table_insert`, `g_hash_table_unref`) using `g_str_hash`/`g_str_equal` resolved with `NativeLibrary.GetExport` as function pointers. Schema: a blittable struct for `SecretSchema` named `org.squill.Secret` with `SECRET_SCHEMA_NONE`, two string attributes. `GError**` is checked and converted into `InvalidOperationException` with the message read from the `GError` struct. `SECRET_COLLECTION_DEFAULT` is the string `"default"`.
- Sync calls run inside `Task.Run` to keep the async contract.
- If a native signature cannot be made to work without unsafe code that raises analyzer warnings, STOP and report; do not suppress warnings.

## Out of scope
- Verification on a real Secret Service (the author is on Windows): cover what can be run here.
- macOS (P2-03).

## Tasks
- [x] Implement the class and the native bindings.
- [x] Tests: on non-Linux the type is not instantiated; a unit test checks the attribute dictionary builder (pure managed helper `LibSecretAttributes.For(key)`). A Linux-only test (skipped elsewhere) performs set/get/delete when `SQUILL_SECRET_SERVICE_TESTS=1`.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet build Squill.slnx -c Release -warnaserror` has no `CA1416`/`SYSLIB1054` warnings.
- The card's report says explicitly that the native path was not executed on this machine (unless it was).

## Suggested commit message
Add libsecret secret store for Linux
