# Contributing to FormWind

Thanks for your interest in FormWind! Bug reports, ideas and pull requests are welcome.

## Reporting issues

- Search the existing issues first.
- Include the FormWind version, the target framework (.NET Framework 4.7.2+ / .NET 8+), Windows version and display
  scaling, the classes involved and, if possible, a minimal form that reproduces the problem.
- Visual problems are much easier to fix with a screenshot. For an existing application, the harness
  (`tools/run-harness.ps1 -SelfTest`) writes logs and screenshots you can attach.
- Security issues: see [SECURITY.md](SECURITY.md), do not open a public issue.

## Development setup

Requirements: Windows, the .NET 10 SDK (it builds all targets; the version is pinned in `global.json`) and, for the
.NET Framework targets, the .NET Framework 4.7.2+ targeting pack (included with Visual Studio).

Before changing code, read [ARCHITECTURE.md](ARCHITECTURE.md): it explains the path from a class string to the screen
and where each concern lives.

To run everything CI runs (build, tests on all targets, the process-isolated categories, coverage check, packing):

```powershell
.\build.ps1
```

`.\build.ps1 -Quick` only builds and tests on .NET 8, for a quick check before committing. Individual commands:

```bash
dotnet build FormWind.sln
```

```bash
dotnet test FormWind.sln
```

Some test categories are skipped by default because they change process-wide state or take long; run them separately:

| Category | Enable with | Notes |
| --- | --- | --- |
| `Performance` | `FORMWIND_PERF=1` | GDI/USER handle and timing measurements; do not run in parallel |
| `UIAutomation` | `FORMWIND_UIA=1` | net472 only |
| `ColorMode` | `FORMWIND_COLORMODE=1` | .NET 9+ only (`Application.SetColorMode`) |
| `Visual` | always on when baselines exist for the environment | update with `FORMWIND_UPDATE_BASELINES=1` and review the PNGs |

Code coverage: see the README (*Code coverage*). CI fails when `FormWind` line coverage drops below the threshold in
`.github/workflows/ci.yml`.

## Pull requests

By submitting a pull request you agree that your contribution is licensed under the [MIT License](LICENSE) of the
project. Do not include code, images or fonts whose license is incompatible with it; list new third-party material in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

- Keep a pull request focused on one change; open an issue first for larger features.
- Add tests: a regression test for every bug fix, and tests for new classes, controls and behavior. Tests that open
  windows belong to `[Collection(UiCollection.Name)]` and run on an STA thread (`Sta.Run`).
- Keep the build free of warnings.
- Update both READMEs (`README.md` in Turkish, `README.en.md` in English) and `CHANGELOG.md` (*Unreleased*).
- Match the style of the surrounding code. Public API documentation (XML comments) and all developer- and
  user-facing strings are in English; user-facing strings go through `TwTexts`. Inline comments may be in Turkish,
  as in most existing files.

## Public API

The public surface is tracked in `src/FormWind/PublicAPI.Shipped.txt` (released) and `PublicAPI.Unshipped.txt`
(changes since the last release). The build warns (RS0016/RS0017) when the public API changes without updating
`PublicAPI.Unshipped.txt`. When a version is released, the contents of `Unshipped` move to `Shipped`; the same applies
to `src/FormWind.Analyzers/AnalyzerReleases.*.md` for analyzer rules.

Keep helpers `internal` unless users need them; extension points (`IStyleApplier`, `TwApplyContext`, `TwStyle`,
`ITwChrome`) are public on purpose.

## Versioning

FormWind follows [Semantic Versioning 2.0](https://semver.org/):

- **Major** (`2.0.0`): breaking changes to the public API (removing or changing members listed in
  `PublicAPI.Shipped.txt`), to class semantics that change the look of existing forms, or dropping a target framework.
- **Minor** (`1.1.0`): new classes, controls, members or options that keep existing code and forms working the same.
- **Patch** (`1.0.1`): bug fixes without API changes. A fix may change rendering only where the previous rendering
  was clearly wrong.

Before 1.0.0, minor versions may contain breaking changes; they are listed under *Changed* or *Removed* in the changelog.
After the first release, package validation (`EnablePackageValidation` with `PackageValidationBaselineVersion`)
checks binary compatibility against the previous version.

## Releasing

1. Move the *Unreleased* changelog entries under the new version with the date.
2. Move `PublicAPI.Unshipped.txt` into `PublicAPI.Shipped.txt` and the analyzer rules into `AnalyzerReleases.Shipped.md`.
3. Update `<Version>` in `Directory.Build.props` (and `PackageValidationBaselineVersion` after the first release).
4. Tag the commit (`v1.2.3`) and push the tag. The release workflow (`.github/workflows/release.yml`) checks that the
   tag matches `<Version>` and that the changelog entry has a date, runs `build.ps1`, pushes the packages to nuget.org
   with Trusted Publishing and creates the GitHub Release with the changelog entry as notes (versions with a `-` suffix
   are marked as prereleases). Publishing waits for the `nuget` environment's approval if one is configured.
5. After the first release, set `PackageValidationBaselineVersion` in `FormWind.csproj` and `FormWind.Krypton.csproj`
   to the released version, so package validation checks binary compatibility against it.

One-time setup for publishing: on nuget.org add a Trusted Publishing policy for the `ModuShip/FormWind` repository,
the `release.yml` workflow and the `nuget` environment; on GitHub create the `nuget` environment and add the
`NUGET_USER` secret (the nuget.org profile name). Reserving the `FormWind` ID prefix on nuget.org is recommended.

The assemblies are strong-named with `FormWind.snk`. The key is public on purpose (strong names are an identity, not a
security feature) and must never change: a new key changes the assembly identity and breaks every application compiled
against FormWind. `AssemblyVersion` follows `<Version>`, so .NET Framework applications that update the package rely on
the binding redirects that SDK-style projects generate. A test or tool project that uses `InternalsVisibleTo` gets the key
automatically (`PublicKey` in `Directory.Build.props`); a project that references an unsigned package sets
`<SignAssembly>false</SignAssembly>`.

The packages' NuGet pages use the short READMEs in `docs/nuget/` (images and links are absolute URLs, because
nuget.org does not resolve repository-relative paths); update them when the README's quick start or the package
features change.
