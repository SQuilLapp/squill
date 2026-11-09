# P12-17 - Optional code signing in the pack script and the release workflow

Status: done
Depends on: P12-16

## Goal
When the owner provides a signing certificate or an Azure Trusted Signing account, releases are signed (avoids the SmartScreen "unknown publisher" warning); without any secret, the workflow still produces unsigned releases exactly as before.

## Design decisions (final)
- Two supported modes, chosen by environment variables only (never by repository files, never by script defaults):
  1. **Azure Trusted Signing**: `AZURE_TRUSTED_SIGNING_METADATA_FILE` (path written by the workflow from the secrets below) passed to `vpk pack --azureTrustedSignFile <file>` (flag name from P12-01).
  2. **PFX certificate**: `SQUILL_SIGN_PFX_BASE64` (base64 of the .pfx) and `SQUILL_SIGN_PFX_PASSWORD`; the workflow decodes the PFX to `$RUNNER_TEMP`, the script passes `--signParams "/f <file> /p <password> /fd sha256 /tr <timestamp url> /td sha256"` (exact `vpk` flag per P12-01). Timestamp URL from `SQUILL_SIGN_TIMESTAMP_URL`, default `http://timestamp.digicert.com`.
- `eng/pack-windows.ps1` gets `-Sign` (switch, off by default): when on, it requires one of the two modes (fails with a message naming the missing variable, never printing a value), passes the flags above to `vpk pack`, then verifies with `Get-AuthenticodeSignature` that `Squill.App.WinUI.exe` inside the package contents and the Setup exe have status `Valid`; fails otherwise. The password and metadata path are never echoed (`Write-Host` guards; PowerShell `-ErrorAction` output of the command line is scrubbed by building the argument string in a variable that is not printed).
- Workflow: a step `Detect signing` sets `SIGN=true` only when the secrets of one mode exist (`secrets.*` read into `env:` of that single step: `${{ secrets.X != '' }}` pattern via an `env` boolean), then passes `-Sign` to the pack script. Documented secret names: `AZURE_TRUSTED_SIGNING_*` (endpoint, account, profile, tenant/client credentials as the action requires, listed after reading the chosen action's README), `SQUILL_SIGN_PFX_BASE64`, `SQUILL_SIGN_PFX_PASSWORD`. Prefer federated identity (OIDC) for Azure over a stored client secret and say so in the docs. Third-party signing actions are pinned to a commit SHA.
- Secrets are never available to `workflow_dispatch` dry runs from forks or to pull requests: the step is `if: github.event_name != 'pull_request'` and the job is not triggered by PRs at all.
- Documentation (the section is completed by P12-20): `docs/release.md` stub "Signing" listing the variables and how to create a Trusted Signing profile; this card writes it only in the script header and the workflow comments.

## Out of scope
- Buying or creating a certificate or Azure account, signing arm64 or other platforms, signing the update feed (Velopack verifies package hashes already).

## Tasks
- [x] `-Sign` in `eng/pack-windows.ps1`, workflow detection and decode steps, header documentation.
- [x] Unsigned path proof: run `rtk pwsh eng/pack-windows.ps1 -Version 0.0.1-test -AllowVersionOverride` still passes (background; no `-Sign`); run with `-Sign` and no variables: it must fail with the missing-variable message and exit non-zero (no pack started: fail before the publish).
- [x] Self-signed positive test, optional and local only: create a throwaway self-signed code-signing certificate in `Cert:\CurrentUser\My`, export a PFX in the scratchpad, run with the PFX variables, expect `Valid` is **not** reached (untrusted root gives `UnknownError`/`NotTrusted`): in that case the script's status check must accept `NotTrusted` only when `SQUILL_SIGN_ALLOW_UNTRUSTED=1` (test aid, documented); remove the certificate and files afterwards.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; the unsigned pack run passes; the `-Sign` without variables run fails fast with the right message.
- `grep` finds no secret value, no `ConvertTo-SecureString -AsPlainText` of a literal, no certificate file in the repository; `rtk git status` shows only the script, the workflow and docs; the throwaway certificate is removed.

## Suggested commit message
Add optional code signing to the pack script and the release workflow

## Report
- `eng/pack-windows.ps1 -Sign` (off by default), self-test extended; `release.yml` got `Detect signing`, `Azure login` (azure/login v3.1.0 pinned to its commit, OIDC, job permission `id-token: write`) and `Azure Trusted Signing metadata` steps, and the Pack step passes `-Sign` only when a complete set of secrets exists. Documented secrets: `SQUILL_SIGN_PFX_BASE64`, `SQUILL_SIGN_PFX_PASSWORD`, or `AZURE_TRUSTED_SIGNING_ENDPOINT/ACCOUNT/PROFILE` + `AZURE_CLIENT_ID/TENANT_ID/SUBSCRIPTION_ID`.
- Deviation: the script (not the workflow) decodes the PFX to a temporary file and deletes it in a `finally`, so a local run and the CI run use the same code; vpk output is scrubbed of the password and the file path.
- Deviation: the self-signed test showed Windows reports `UnknownError` (not `NotTrusted`) for an untrusted self-signed root, so `SQUILL_SIGN_ALLOW_UNTRUSTED=1` accepts both; `NotSigned`/`HashMismatch` always fail.
- Proof: unsigned pack of 0.0.1-test passes; `-Sign` without variables fails before the publish naming the variables; with a throwaway self-signed PFX the Setup exe and the app exe inside the package were signed (status accepted only with the test aid), no password or path leaked in the output; the certificate, the PFX and the temporary files were removed.
- Not verified: a real Azure Trusted Signing or CA certificate run (needs the owner's account); the CI dry run of the release workflow after this card (see commit history for the run).
