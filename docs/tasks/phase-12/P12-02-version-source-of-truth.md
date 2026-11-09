# P12-02 - Version: single source of truth, assembly version and AppVersion

Status: done
Depends on: none

## Goal
One file defines the version; every assembly, the exe's file/product version, the Velopack package and the running app read it. No reflection.

## Design decisions (final)
- `eng/Version.props`: `<Project><PropertyGroup><SquillVersion>0.1.0</SquillVersion></PropertyGroup></Project>` (a one-line comment says: "the only place a version is written; the release tag is `v` + this value").
- `Directory.Build.props` imports it (before the `Version` use) and sets `Version` = `$(SquillVersion)` unless already set on the command line (so `-p:Version=` still wins for the local verification builds of P12-19), `InformationalVersion` = `$(Version)` **without** the `+commit` suffix (`IncludeSourceRevisionInInformationalVersion` false; deterministic and identical to the tag), `Product` = `SQuilL`, `Company`/`Authors` = `SQuilL`, `Copyright` = `Copyright (c) SQuilL contributors`.
- `AppVersion` (public static class in `Squill.Core`, namespace `Squill.Core`): `public const string Current` holding the version string. Generated at build time by a target in `src/Squill.Core/Squill.Core.csproj` (`WriteLinesToFile` into `$(IntermediateOutputPath)AppVersion.g.cs`, added to `Compile`; `Overwrite=true`, `WriteOnlyWhenDifferent=true`, an `<ItemGroup>` `FileWrites` entry) so there is no reflection and no `AssemblyInformationalVersionAttribute` read. XML doc on the generated class and member (the generator writes them; CS1591 is an error).
- `eng/get-version.ps1`: prints the value of `SquillVersion` (parse the XML, no MSBuild invocation) and exits 1 when it is not valid SemVer 2 (`^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$`). Parameter `-Tag <string>` (for example `v0.1.0`): exits 1 with a clear message when `-Tag` is not exactly `v` + the version. Used by P12-05 and P12-16.
- The WinUI head's exe gets the file/product version automatically from `Version` (no extra property); check it in the acceptance criteria.

## Out of scope
- SemVer comparison (P12-03), pack script changes (P12-05), workflow (P12-16).

## Tasks
- [x] `eng/Version.props`, imports in `Directory.Build.props`, generator target, `AppVersion`.
- [x] `eng/get-version.ps1` with `-Tag` check.
- [x] Test in `tests/Squill.Core.Tests`: `AppVersion_Current_MatchesVersionProps` (reads `eng/Version.props` from the repository root found by walking up from `AppContext.BaseDirectory`, compares with the constant); `AppVersion_Current_IsValidSemVer` (regex above).
- [x] Document the rule in `README.md` build section (two lines) and `docs/tasks/README.md` conventions (one bullet: "never type a version outside `eng/Version.props`").

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green.
- `rtk pwsh eng/get-version.ps1` prints `0.1.0`; `-Tag v0.1.0` exits 0; `-Tag v0.2.0` and `-Tag 0.1.0` exit 1.
- `rtk dotnet build src/Squill.App.WinUI -c Release -p:Version=9.9.9-test` produces an exe whose `(Get-Item <exe>).VersionInfo.ProductVersion` is `9.9.9-test`.
- Sonar: no duplicated block between the generator and existing targets.

## Suggested commit message
Add a single version source and a reflection-free AppVersion
