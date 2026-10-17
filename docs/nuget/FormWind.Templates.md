# FormWind.Templates

`dotnet new` template for [FormWind](https://www.nuget.org/packages/FormWind): a WinForms app with ready-made screens styled with Tailwind-style classes. Sign-in (validated fields), list-detail (searchable `TwDataList` with a detail card) and settings; the light/dark switch is shared across forms.

```bash
dotnet new install FormWind.Templates
dotnet new formwind-app -n MyApp
```

| Option | Values |
| --- | --- |
| `--Framework` | `net8.0-windows` (default), `net9.0-windows`, `net10.0-windows`, `net472` |
| `--Preset` | `tailwind` (default), `fluent`, `antd`, `material` |
| `--Palette` | `v3` (default), `v4` |
| `--Krypton` | `true` adds [FormWind.Krypton](https://www.nuget.org/packages/FormWind.Krypton) and binds the Krypton palette to the main form's theme |
| `--FormWindVersion` | FormWind package version (default: the version of this template package) |

```bash
dotnet new formwind-app -n MyApp --Framework net8.0-windows --Preset fluent --Krypton true
```

Forms are built in code without the Designer and use `AutoScaleMode.Dpi`.

[Documentation on GitHub](https://github.com/ModuShip/FormWind/blob/main/README.en.md#project-template) · MIT License.
