# Phase 21 recipe - competitive gaps (DBeaver / DataGrip)

Shared rules for every P21 card. A card says "recipe N" for a section here. Audit of the repository at `d5bf4c6` (branch `feat/explorer-filter-popup`, after P20-43).

## 1. Where the work comes from

The owner compared SQuilL with DBeaver and DataGrip on 2026-11-27 and asked to card **every** missing feature, to be developed later. These are owner-requested ideas, **not scheduled**: no card here is started before the owner picks it. Each card says what already exists ("Exists today") and is scoped to the gap only.

## 2. Rules for code cards

- Tests first: write the failing test, see it fail, then implement. A card whose premise is false in the code stops with a report (Done notes) and a proposed card fix.
- No `NoWarn`, no `#pragma warning disable`, no `.editorconfig` exemption, no reflection, JSON only through source-generated contexts. A card adds only the NuGet package it names (version recorded in `docs/architecture.md`).
- Every write to a database goes through `SqlRunner` / `StatementScope`, the session transaction and `ProductionRequestBuilder` (phase 15); a card never calls `session.Connection.ExecuteAsync` for a write.
- Every user-visible text goes through `UiText` (both catalogs, parity and used-keys tests).
- Nothing leaves the machine (network call, file outside the data folder) without a visible user action; secrets only in `ISecretStore` (`SecretKeys`).
- Common acceptance: `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings (head cards also `Squill.Windows.slnx`), `rtk bypass dotnet test --solution Squill.slnx -c Release` green, catalog parity and string scanner green, XML doc check by grep. Close everything started (`dotnet build-server shutdown`, apps, containers).

## 3. Rules for head cards

- WinUI card before its Avalonia twin; never start a head card before its Presentation card.
- Head code changed: the AOT publish of that head (`eng/check-aot-warnings.ps1 -Head WinUI|Avalonia`, baseline warnings only), the app starts, and the feature is driven on the **published exe** with `tools/visual/Drive-Window.ps1` (P20-01) and captured in dark and light (French for one, English for the other). Read every PNG; captures are not committed. A step that cannot be driven is written `not driven (<why>)` in the Done notes; never stretch a claim.
- WinUI: no `ItemsSource` on code-built menus (AOT trap of P20-27), no `Application.Current.Resources[...]` brush lookup (`ThemeLookupScanTests`). Avalonia: popups are invisible to the capture tools (P20-19), check state through the view-model instead.
- The **last** head card of a feature writes the README section (or line) and a `CHANGELOG.md` line under the unreleased section. No separate docs card in this phase.

## 4. Rules for engine cards

- Docker-gated tests (`SQUILL_DOCKER_TESTS=1`) run with `rtk pwsh eng/run-docker-tests.ps1 -Engine <name>` on Podman ([phase 11 recipe](../phase-11/verification-recipe.md)); a skipped Docker test is never reported as verified. New engines are added as a new `-Engine` value, not to `all` when the image is larger than 2 GB (precedent: `oracle`).
- A new driver package is first proven by an AOT probe console (`tools/Squill.AotProbe.<Engine>`, precedent `tools/Squill.AotProbe.*` and [docs/poc/aot-drivers.md](../../poc/aot-drivers.md)): publish, zero IL2xxx/IL3xxx from SQuilL code, start, and a real query against a container. If the probe fails, the fallback is the bridge process of [docs/poc/oracle-bridge.md](../../poc/oracle-bridge.md).
- The support matrix of [ADR 0008](../../adr/0008-engine-parity.md) is updated by the card that changes a row.

## 5. Rules for proof-of-concept and decision cards

- Output: a report under `docs/poc/` or an ADR under `docs/adr/` (next number: 0018), plus at most a throw-away probe under `tools/`. No product code.
- Every claim about a third-party driver is either measured in the card (probe output quoted) or marked "not verified". Licences are stated (some IBM and cloud drivers need one).
- The report ends with a verdict (`go`, `go through the bridge`, `no go`) and the cards to write next.

## 6. Privacy rules (AI and every network feature)

- Off by default, opt-in in Settings, provider key in `ISecretStore`.
- What is sent is shown before it is sent (exact payload), and the user confirms; data rows are never sent by default.
- No telemetry, no background calls.
