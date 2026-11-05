# P11-06 - Core: connection options (TLS, timeouts)

Status: done
Depends on: none

## Goal
A connection profile can say how to secure the connection and how long to wait; today `ConnectionProfile` only has host, port, database, user, file and SSH, so TLS is whatever the driver defaults to and SQL Server trusts the certificate only for the literal host `localhost`.

## Design decisions (final)
- New enum `TlsMode { Default, Disable, Require, VerifyFull }` in `Squill.Core` (XML doc on each value; `Default` = driver default so existing profiles behave as before).
- `ConnectionProfile` gets defaulted trailing members: `TlsMode Tls = TlsMode.Default`, `bool TrustServerCertificate = false`, `int ConnectTimeoutSeconds = 15`, `int CommandTimeoutSeconds = 0` (0 = no limit). Older profile files without the fields load unchanged (add a test with a JSON file written by the previous version, from a literal in the test).
- `DesignValidator`/profile validation (look at how `ConnectionEditorViewModel` validates today) rejects negative timeouts and values above 86400 with message codes registered in `CoreMessageCodes` (see P10-09 for the pattern, both catalogs).
- The mapping to each driver is P11-07; this card is the model, JSON and validation only.

## Out of scope
- Driver connection strings, UI.

## Tasks
- [x] Enum, record members, validation, message codes with fr/en strings, tests (round trip through `ProfileJsonContext`, old-file load, validation).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green (localization parity tests included).

## Suggested commit message
Add TLS and timeout options to the connection profile

## Notes
- Validation lives in `ConnectionProfile.ValidateOptions()` (returns `DesignIssue`s with codes `connection.connectTimeoutRange` / `connection.commandTimeoutRange`, arg = 86400), because `ConnectionEditorViewModel` does not expose the new fields yet (UI is P11-08).
- Overlap with P11-04 (SQL Server trusting 127.0.0.1/::1): untouched; P11-07 maps `Tls`/`TrustServerCertificate` on top of it.
