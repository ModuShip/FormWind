# FormWind

**Modernize your existing WinForms application without rewriting it:** a Tailwind-style styling layer for the standard controls. Controls stay as they are; pick a preset (Fluent, Ant Design, Material) and turn on `AutoStyle` to style a form without writing classes, then add classes per control where needed.

```
btnSave  →  Classes: "bg-blue-600 text-white px-4 py-2 rounded-md font-bold hover:bg-blue-700"
```

| Light | Dark |
| --- | --- |
| ![Light](https://raw.githubusercontent.com/ModuShip/FormWind/main/docs/showcase-light.png) | ![Dark](https://raw.githubusercontent.com/ModuShip/FormWind/main/docs/showcase-dark.png) |

- Targets .NET Framework 4.7.2+ and .NET 8+ (`net472`, `net8.0-windows`); tested on .NET Framework, .NET 8, 9 and 10
- No dependencies beyond .NET (only `System.Text.Json` on `net472`)
- State variants: `hover:`, `focus:`, `active:`, `disabled:`, `checked:`, `dark:` and combinations (`dark:hover:`)
- Flex and grid layout (`flex`, `gap-4`, `justify-between`, `flex-1`, `w-1/2`, `grid-cols-3`, `col-span-2`)
- Presets (Tailwind, Fluent, Ant Design, Material), component classes (`btn-primary`, `card`) and a JSON theme file
- Controls (`TwButton`, `TwTextBox`, `TwSelect`, `TwDataList` …), DataGridView styling and lightweight charts
- Dark title bars, scrollbars and tooltips; WCAG contrast audit, Windows High Contrast and UI Automation support
- Designer support: a `Classes` property, smart tags, a class builder and the FW001/FW002 analyzers

## Quick start

1. In `Program.Main`, load the theme before any form is opened (the default theme is used if the file does not exist):

   ```csharp
   TwTheme.Current = TwTheme.LoadIfExists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tw.config.json"));
   ```

2. Drag `TailwindProvider` from the Toolbox onto the form. If it is missing: Toolbox → right-click → **Choose Items** → `FormWind.dll`.
3. Select controls and fill in `Classes on tailwindProvider1`. The Designer generates:

   ```csharp
   this.tailwindProvider1.SetClasses(this.btnSave, "btn-primary");
   ```

4. Dark mode: `tailwindProvider1.DarkMode = true;`

To start without writing classes, choose a preset in `tw.config.json` and turn on `AutoStyle`:

```jsonc
{ "preset": "fluent" }   // tailwind (default), fluent, antd, material
```

```csharp
tailwindProvider1.AutoStyle = true;
```

| Tailwind | Fluent | Ant Design | Material |
| --- | --- | --- | --- |
| ![Tailwind](https://raw.githubusercontent.com/ModuShip/FormWind/main/docs/preset-tailwind.png) | ![Fluent](https://raw.githubusercontent.com/ModuShip/FormWind/main/docs/preset-fluent.png) | ![Ant Design](https://raw.githubusercontent.com/ModuShip/FormWind/main/docs/preset-antd.png) | ![Material](https://raw.githubusercontent.com/ModuShip/FormWind/main/docs/preset-material.png) |

The looks are inspired by the named design systems; they are not those systems and contain no logos or assets.

## Related packages

- [FormWind.Krypton](https://www.nuget.org/packages/FormWind.Krypton): classes on Krypton Toolkit controls and a Krypton palette that follows the FormWind theme.
- [FormWind.Templates](https://www.nuget.org/packages/FormWind.Templates): `dotnet new formwind-app`, an app with sign-in, list-detail and settings screens.

## Preview status and known limitations

FormWind is in preview (0.x): minor versions may contain breaking changes; they are listed in the [changelog](https://github.com/ModuShip/FormWind/blob/main/CHANGELOG.md). The extension points tied to System.Drawing and WinForms types (`IStyleApplier`, `TwStyle` …) are experimental and report FW9001 until it is suppressed.

- Controls that paint themselves (e.g. owner-drawn grids) ignore FormWind's styles and may stay light in dark mode (owner-drawn grids: `TwGrid.EnableDarkInversion`).
- The multi-line `Classes` editor window is available only in the .NET Framework Designer; the .NET 8+ Designer has the drop-down, smart tag and class builder.

## Documentation and license

The full class reference, theme file format, DataGridView, charts, accessibility and Designer documentation are in the [README on GitHub](https://github.com/ModuShip/FormWind/blob/main/README.en.md). Issues and feedback: [GitHub Issues](https://github.com/ModuShip/FormWind/issues).

MIT License. Third-party material (the Tailwind CSS color palette) is listed in `THIRD-PARTY-NOTICES.md` in the package. FormWind is not affiliated with or endorsed by Tailwind Labs.
