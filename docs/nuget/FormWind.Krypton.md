# FormWind.Krypton

[FormWind](https://www.nuget.org/packages/FormWind) for [Krypton Toolkit](https://github.com/Krypton-Suite/Standard-Toolkit) applications. Krypton's ribbon, docking and navigator stay in place; FormWind's presets and dark mode reach the Krypton controls in two ways, which can be combined.

| Krypton + FormWind (Fluent) | Krypton + FormWind (Material, dark) |
| --- | --- |
| ![Fluent](https://raw.githubusercontent.com/ModuShip/FormWind/main/docs/krypton-fluent.png) | ![Material dark](https://raw.githubusercontent.com/ModuShip/FormWind/main/docs/krypton-material-dark.png) |

**1. Theme bridge: no classes needed.** `TwKryptonTheme` builds a Krypton palette from the FormWind theme and makes it Krypton's global palette. Every Krypton control without classes follows the chosen preset and dark mode:

```csharp
using FormWind.Krypton;

KryptonApplier.Register();                                   // in Program.Main
var palette = TwKryptonTheme.Follow(tailwindProvider1);      // in the main form; palette.Dispose() when it closes
```

To manage a fixed palette yourself, `TwKryptonTheme.CreatePalette(theme, dark)` returns a `KryptonCustomPaletteBase`.

**2. Classes per control.** `KryptonApplier` writes classes to Krypton's state palettes: base → `StateCommon`, `hover:` → `StateTracking`, `active:` → `StatePressed`, `focus:` → `OverrideFocus`, `disabled:` → `StateDisabled`, `checked:` → `StateChecked*`. Supported: buttons and check buttons, panels and labels, check boxes and radio buttons, text and input controls, `KryptonComboBox` (`item:`, `menu:`), `KryptonGroupBox` (`caption:`), `KryptonHeaderGroup` (`header:`), list boxes (`item:`, `selected:`), `KryptonForm` (`caption:`) and `KryptonDataGridView`.

## Versions

Built against Krypton Toolkit 100.25.11.328 (the package depends on that version or later); CI also tests 105.26.7.201. .NET Framework applications that use a newer Krypton need binding redirects; SDK-style projects generate them. Targets `net472` and `net8.0-windows`.

`KryptonApplier` builds on FormWind's experimental applier API (FW9001), so this package is released together with FormWind and uses the same version.

## Documentation and license

[Krypton Toolkit section of the README](https://github.com/ModuShip/FormWind/blob/main/README.en.md#krypton-toolkit) · [Changelog](https://github.com/ModuShip/FormWind/blob/main/CHANGELOG.md) · MIT License.
