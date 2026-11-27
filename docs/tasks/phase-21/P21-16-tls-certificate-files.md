# P21-16 - Core and Data: TLS certificate files (CA, client certificate and key)

Status: done
Depends on: none

## Goal
Connect to servers that use a private CA or require a client certificate (mutual TLS), as DBeaver and DataGrip allow.

Exists today: `ConnectionProfile.Tls` (`TlsMode`: `Default`, `Disable`, `Require`, `VerifyFull`) and `TrustServerCertificate`, mapped by the four providers (P11-06, P11-07) and shown in both connection editors (P11-08, P11-09). Gap: no CA file, no client certificate, no client key, no SQL Server host name in certificate.

## Design decisions (final)
- New optional profile fields (defaulted, older files load unchanged): `TlsCaFile`, `TlsClientCertFile`, `TlsClientKeyFile` (PEM paths), `TlsServerName` (expected host name when it differs from `Host`, e.g. behind an SSH tunnel). The client key passphrase is a secret: `SecretKeys.TlsKeyPassphrase(id)`.
- Mapping: Npgsql `RootCertificate`, `SslCertificate`, `SslKey`, `SslPassword` (check the slim builder exposes them; otherwise `UseClientCertificatesCallback` / `UseUserCertificateValidationCallback` with `X509Certificate2.CreateFromPemFile`); MySqlConnector `SslCa`, `SslCert`, `SslKey`; SqlClient `HostNameInCertificate` and `ServerCertificate` (client certificates are not supported by SqlClient: the field is disabled for SQL Server with a note); SQLite: n/a.
- Validation (`ValidateOptions`): files must exist when set; a key without a certificate is an error.
- Over an SSH tunnel, `TlsServerName` defaults to the profile host so `VerifyFull` keeps working against `localhost:<forwarded port>`.

## Out of scope
- The editor fields (P21-17), Windows certificate store selection.

## Tasks
- [x] Profile JSON round trip and validation tests.
- [x] Docker-gated tests: PostgreSQL and MySQL containers started with a test CA and `clientcert=verify-full` / `REQUIRE X509`; connect with and without the files.

## Acceptance criteria
- Recipe 2 common acceptance; Docker-gated TLS tests green for PostgreSQL and MySQL.

## Risks
- Npgsql slim builder may need an explicit opt-in for some TLS features (`EnableTransportSecurity`): check before writing the mapping.

## Open points (defaults)
- None.

## Suggested commit message
Add CA, client certificate and key files to the TLS options of a connection

## Done notes
- Premise checked: `Tls`, `TrustServerCertificate` and the four provider mappings exist as stated; the Npgsql slim builder exposes the connection-string options and `UseSslClientAuthenticationOptionsCallback` (no callback with `X509Certificate2` needed).
- `ConnectionProfile` gets `TlsCaFile`, `TlsClientCertFile`, `TlsClientKeyFile`, `TlsServerName` (defaulted, older files load unchanged) and a runtime-only `TlsKeyPassphrase` (`[JsonIgnore]`, never written). It is read by `SessionManager` through the new default interface method `IProfileStore.GetTlsKeyPassphraseAsync` (`SecretKeys.TlsKeyPassphrase`; `JsonProfileStore` implements it and deletes the secret with the profile). Saving the passphrase is P21-17 (the editor owns the field; `SaveAsync` is unchanged).
- Mapping: PostgreSQL `RootCertificate`/`SslCertificate`/`SslKey`/`SslPassword` and `TlsServerName` through `UseSslClientAuthenticationOptionsCallback` (`TargetHost`); MySQL `SslCa`/`SslCert`/`SslKey` only (MySqlConnector has neither a server-name override nor encrypted keys: `TlsServerName` and the passphrase are ignored, recorded as a `shortcut:`); SQL Server `ServerCertificate` (from `TlsCaFile`) and `HostNameInCertificate`, client files ignored (P21-17 disables them).
- Over an SSH tunnel `SessionManager` sets `TlsServerName` to the profile host when empty.
- `ValidateOptions` reports a missing file (`connection.tlsFileMissing`, path as argument) and a key without certificate (`connection.tlsKeyWithoutCertificate`); both messages in the FR and EN catalogs.
- Docker tests (throw-away CA from `tests/Shared/TestPki.cs`, generated at test time): PostgreSQL with `clientcert=verify-full` (CA + client certificate, encrypted key + passphrase, refused without client certificate / without CA / without server name) and MySQL with `REQUIRE X509` (connect, refused without client certificate / without CA). SQL Server not run (the card asks PostgreSQL and MySQL; its mapping is covered by a connection-string test).
