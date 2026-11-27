# P21-16 - Core and Data: TLS certificate files (CA, client certificate and key)

Status: todo
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
- [ ] Profile JSON round trip and validation tests.
- [ ] Docker-gated tests: PostgreSQL and MySQL containers started with a test CA and `clientcert=verify-full` / `REQUIRE X509`; connect with and without the files.

## Acceptance criteria
- Recipe 2 common acceptance; Docker-gated TLS tests green for PostgreSQL and MySQL.

## Risks
- Npgsql slim builder may need an explicit opt-in for some TLS features (`EnableTransportSecurity`): check before writing the mapping.

## Open points (defaults)
- None.

## Suggested commit message
Add CA, client certificate and key files to the TLS options of a connection
