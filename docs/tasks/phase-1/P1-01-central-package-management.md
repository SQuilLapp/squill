# P1-01 - Central package management and editorconfig

Status: done
Depends on: none

## Goal
Pin every NuGet version in one place and set code-style defaults before any package-using project exists.

## Design decisions (final)
- `Directory.Packages.props` at the repo root with `ManagePackageVersionsCentrally=true`; `PackageReference` items in csproj files carry no `Version`.
- Initial entries: `xunit.v3` 4.0.2, `Microsoft.NET.Test.Sdk` 18.0.0, `xunit.runner.visualstudio` 3.1.5 (the versions currently in `tests/Squill.DesignSystem.Tests/Squill.DesignSystem.Tests.csproj`). Later cards add the entries they need, with the versions of `docs/architecture.md`.
- `.editorconfig` at the root: `charset = utf-8`, `indent_style = space`, `indent_size = 4` (2 for xml/json/props/csproj/axaml/xaml/yml), `insert_final_newline = true`, `csharp_style_namespace_declarations = file_scoped:warning`.
- `tests/.editorconfig` (not `root = true`) sets `dotnet_diagnostic.CA1707.severity = none`; remove the `NoWarn CA1707` from the test csproj.

## Out of scope
- Adding packages other than the three above.
- Changing `Directory.Build.props` or `global.json`.

## Tasks
- [x] Create `Directory.Packages.props` with the three entries.
- [x] Strip `Version=` from the test csproj `PackageReference` items.
- [x] Add `.editorconfig` and `tests/.editorconfig`; remove `NoWarn` from the test csproj.
- [x] Run the build/test commands; fix style violations the new `.editorconfig` surfaces in `src/` and `tools/` (formatting only).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add central package management and editorconfig
