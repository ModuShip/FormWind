# Changelog

All notable changes to FormWind are documented in this file. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](CONTRIBUTING.md#versioning).

## [Unreleased]

## [0.1.0-preview.1] - 2026-10-17

First public preview.

### Added

- **Utility classes for WinForms** through `TailwindProvider`: colors with opacity, typography and font weights, spacing,
  sizes, borders and corners, shadows and rings, gradients, cursors, visibility.
- **Variants:** `hover:`, `focus:`, `focus-visible:`, `active:`, `disabled:`, `checked:`, `invalid:`, `group-hover:`,
  `dark:`, responsive `sm:` … `2xl:`, `rtl:`/`ltr:` and `forced-colors:`, and their combinations.
- **Layout:** flex and grid on `TwPanel` (gap, justify, align, wrap, basis, grow/shrink, `col-span`, `row-span`, `grid-rows`),
  mirrored for right-to-left.
- **Themes:** JSON theme file (`tw.config.json`) with custom colors, components, font stacks, bundled fonts and icons;
  Tailwind v3 palette by default and the Tailwind v4 (OKLCH) palette with `"palette": "v4"`.
- **Presets:** `TwPreset` (Tailwind, Fluent, Ant Design, Material), `new TwTheme(TwPreset.Fluent)` or `"preset": "fluent"`
  in the theme file. All presets define the same components and pass the contrast audit in light and dark mode.
- **Semantic colors** `primary`, `surface`, `danger`, `success`, `warning` (`TwTheme.SemanticColors`); the default
  components use them, so defining one in the theme file recolors every component.
- **Components:** `btn-primary`, `btn-secondary`, `link`, `input`, `select`, `toggle`, `range`, `card`, `page`, `sidebar`,
  `nav-item`, `title`, `muted`, `badge`, `alert-info`, `alert-success`, `alert-warning`, `alert-danger`, `list`, `tabs`,
  `menu` and more (`TwTheme.DefaultComponents`).
- **AutoStyle:** `TailwindProvider.AutoStyle` and `ContainerControl`: controls get the component mapped to their type
  (`TwTheme.AutoStyle`, `"autoStyle"` in the theme file); their own classes are added on top.
- **Controls:** `TwButton`, `TwPanel`, `TwTextBox`, `TwNumberBox`, `TwSelect`, `TwDatePicker`, `TwCalendar`, `TwCheckBox`,
  `TwRadioButton`, `TwSwitch`, `TwSlider`, `TwProgress`, `TwTabStrip`, `TwTabControl`, `TwDataList`, `TwListBox`,
  `TwCheckedListBox`, `TwTreeView`, `TwPager`, `TwField`, `TwDialog`, `TwToast`, `TwToolTip`, `TwIcon`, `TwPictureBox`.
- **Standard controls:** ToolStrip/menu renderer, DataGridView part prefixes, column classes, cell types, row rules,
  sorting, filtering, column chooser, virtual rows; framed `TextBox` and painted `DropDownList` `ComboBox`; dark title
  bars, Windows 11 backdrops, dark native scrollbars and tooltips.
- **DataGridView:** search with highlighting (`TwGrid.SetSearchText`, `AttachSearchBox`, `match:`), a search box and
  min/max range boxes in the filter menu (`ITwGridSource`), grouping with collapsible headers (`SetGroupBy`, `group:`),
  a totals row (`TwGridFooter`, `footer:`), row details (`SetRowDetails`, `TwExpanderColumn`), saved layouts
  (`SaveLayout`/`LoadLayout`), CSV and clipboard export, `table-sm`/`table-lg`/`table-striped`/`table-bordered`, and
  dark mode for owner-drawn grids by inversion (`EnableDarkInversion`). New cell types: `TwSparklineColumn`,
  `TwDateColumn` (relative dates), `TwNumberColumn`, `TwLinkColumn`, `TwTagsColumn`, `TwActionsColumn`.
- **Charts:** `TwChart` (line, area, bar, stacked bars, mixed series), `TwDonutChart`, `TwSparkline` and the KPI card
  `TwStat`, drawn without dependencies with a validated colorblind-safe palette, tooltips, keyboard navigation and screen
  reader items; the `chart` and `stat` components and `label:`/`divide-*`/`accent-*` styling.
- **Events:** `TailwindProvider.StyleError`, `DarkModeChanged` and `ThemeChanged`.
- **Accessibility:** UI Automation trees for owner-drawn controls, WCAG contrast audit (`TwContrast`), Windows High
  Contrast support (`RespectHighContrast`), keyboard support throughout.
- **Localization:** `TwTexts` with English and Turkish texts, chosen from the UI culture.
- **Robustness:** styling errors are reported through `TailwindProvider.StyleError` instead of crashing the application.
- **Designer:** smart tags with quick component selection (choosing a component replaces only the current one and keeps
  the other classes), a class builder (colors, padding, border, corners, text, hover and dark-mode colors) that also
  works in the .NET 8+ out-of-process Designer, a live preview, and the project's theme (`build/FormWind.targets` embeds
  `tw.config.json` into the application's assembly; `FormWindThemeFile`, `FormWindEmbedTheme`).
- **Krypton Toolkit** (`FormWind.Krypton`, Krypton Toolkit 100.25.11.328 or later; CI also tests 105.26.7.201):
  `KryptonApplier` maps classes to the state palettes of buttons (`KryptonCheckButton` with `checked:`), check boxes,
  radio buttons, text and input controls, `KryptonComboBox` (`item:`, `menu:`), `KryptonGroupBox` (`caption:`),
  `KryptonHeaderGroup` (`header:`), `KryptonListBox`, `KryptonCheckedListBox` (`item:`, `selected:`) and `KryptonForm`
  (`caption:`); `TwKryptonTheme.CreatePalette` and `TwKryptonTheme.Follow` build Krypton's global palette from the
  FormWind theme, so Krypton controls without classes follow the preset and dark mode.
- **Tooling:** FW001 and FW002 Roslyn analyzers, Designer file converter (`tools/FormWind.Convert`), real-application
  harness (`tools/run-harness.ps1`), website with a landing page and documentation in English and Turkish (`website/`, built with
  `website/build.ps1`), class reference generator (`tools/FormWind.Docs`), `dotnet new formwind-app`
  template (`FormWind.Templates`; `--Preset`, `--Palette`, `--Krypton`), `build.ps1` running the same steps as CI.
- **Samples:** `samples/FormWind.Showcase` (with a presets form, `--presets`) and `samples/FormWind.Krypton.Showcase`.
- Compatibility with .NET 9+ `Application.SetColorMode`.
- Targets .NET Framework 4.7.2 and .NET 8; tested on .NET Framework, .NET 8, 9 and 10.
- MIT License; `THIRD-PARTY-NOTICES.md` is included in the packages.
- Strong-named assemblies (`PublicKeyToken=1cea47bdc82de2bf`), so strong-named .NET Framework applications can reference
  the packages.
- **API stability:** the extension points tied to System.Drawing and WinForms types (`IStyleApplier`, `TwApplyContext`,
  `TwAppliers`, `WinFormsApplier`, `TwStyle`, `TwParsed`, `TwOriginal`, `TwTheme.Parse` and the helpers that take a
  `TwStyle`) are experimental: using them reports FW9001 until it is suppressed. `"version"` in the theme file
  (`TwTheme.FileVersion`, currently 1) rejects files written for a newer format. The meaning of the class syntax is
  pinned by golden tests (`tests/FormWind.Tests/Golden/parse.txt`). The public API of both packages is tracked.
  Only FormWind itself creates `TwApplyContext`, and the Designer types (`FormWind.Design`) are internal.

### Known limitations

- Controls that paint themselves (e.g. owner-drawn grids) ignore FormWind's cell styles and may stay light in dark mode.
- The multi-line `Classes` editor window ("…") is available only in the .NET Framework Designer; in the .NET 8+
  Designer, `Classes` is a drop-down with free text, and the smart tag and class builder work as usual.

[Unreleased]: https://github.com/ModuShip/FormWind/compare/v0.1.0-preview.1...HEAD
[0.1.0-preview.1]: https://github.com/ModuShip/FormWind/releases/tag/v0.1.0-preview.1
