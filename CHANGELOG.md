# Changelog

All notable changes to FormWind are documented in this file. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](CONTRIBUTING.md#versioning).

## [Unreleased]

### Added

- `TwTheme.SuggestClass` returns the closest recognized class for an unknown one, variant prefixes included
  (`hovr:bg-blu-600` → `hover:bg-blue-600`). The Debug output for unknown classes, the class editor and the smart tag
  show the suggestions.
- `website/llms.txt`: a summary of the classes, components, variants and rules for coding agents, served at the site
  root and checked against the parser by tests.

## [0.1.1-preview.1] - 2026-10-20

### Added

- `TwAutoTheme.Install` styles an existing application with one call in `Program.Main`: every form that opens gets a
  provider with `AutoStyle` (`DarkMode`, `FollowWindowsTheme`, `Filter`, `FormAttached`, `GetProvider`); forms that
  already have a provider are left to it.
- `text-*` sizes follow Windows' text size setting (Accessibility → Text size), like `rem` sizes in a browser
  (`TwFonts.RespectTextScale`, on by default).
- Standard `DateTimePicker` is painted dark on a dark background (edit area, buttons and drop-down calendar) and gets the
  `select` component from `AutoStyle`; `NumericUpDown`/`DomainUpDown` get `input`.
- A dark `DropDownList` `ComboBox` without border classes is painted with a 1 px border instead of staying light.
- Keyboard focus rectangles are drawn on FormWind's tab strip and `DropDownList` box while Windows shows focus cues.
- `formwind-convert <folder> --audit [--report file.md]`: a read-only scan before migrating that lists code to check by
  hand (colors set in code, list item colors, owner-drawn controls, fixed-size buttons with images, `SetWindowTheme`,
  RTF).
- `build/FormWind.targets` copies the theme file to the output and publish folders (`FormWindCopyTheme`) and turns on
  binding redirects for old-style .NET Framework projects, which failed at startup without them.
- Harness: `FORMWIND_MENU` opens forms through the application's own menu items, `FORMWIND_DPI` (`-Dpi`) moves styled
  forms to another DPI, `FORMWIND_SELFTEST_DELAY` waits before the first capture; applications that already use FormWind
  keep their own provider and are audited instead of restyled.
- FormWind can be used by an application's plugin (e.g. KeePass): on .NET Framework, dependencies that the host has no
  binding redirects for are loaded from FormWind's folder (see "Plugins and shared processes" in the README).

- `AutoStyle` leaves controls whose background was copied from their parent (typical of application theme engines) to
  follow the parent's new background (`FollowParentBackColor`, on by default); `TailwindProvider.InheritCopiedBackColors`
  does the same for classes assigned in code.
- `PropertyGrid`, standard `TabControl`/`TabPage` and `ProgressBar` are styled (`properties`, `tab-control`, `tab-page`
  and `range` components, also mapped by `AutoStyle`).
- `TailwindProvider.GetColors` and `GetCurrentColors` resolve class colors for code that paints controls itself.
- `TwContrastIssue.Cause` (`TwContrastCause.KeptBackground`) explains low contrast caused by an application background
  kept under a class text color.
- Harness: `TIMER STARVATION` and `UI BLOCKED` log lines; `FORMWIND_EXIT_AFTER` runs on a background thread.
- `LinkLabel` links take `accent-*` (`link-label` component, mapped by `AutoStyle`); the contrast audit checks them.
- Dark mode darkens ListView column and group headers.
- Harness: runs on every UI thread of applications with several; `Install` can be called at the start of `Main`;
  the application's error dialog details are logged.

### Changed

- `transition` no longer animates the first application of classes; theme, state and class changes still animate.
- Disabled `TextBox` and `MaskedTextBox` keep the classes' colors (Windows follows their background while disabled);
  only `RichTextBox` falls back to Windows' disabled look.
- `DarkToolTips` darkens only the tooltips of forms the provider styles; it was thread-wide, so a plugin also darkened
  the host's tooltips.
- In High Contrast, Windows draws tab strips, progress bars, title bars, tooltips, link colors and native themes again.
- Invalid theme files throw `FormatException` (the cause is in `InnerException`) instead of `JsonException` or
  `InvalidOperationException`.

### Fixed

- The design-time preview no longer writes its values (flat style, `UseVisualStyleBackColor`, link colors, grid cell
  styles and fonts) to Designer.cs, and turning the preview off restores the controls.
- Standard buttons with a background class keep a border in High Contrast (they looked like plain text).
- With visual styles off, the dark ListView header text is left alone (it was light text on the classic light header).
- Colors the application resets to their default (`ResetBackColor`) go back to the classes.
- Disabled `GroupBox` captions, `ListView`s and editable `ComboBox`es are drawn in readable colors in dark mode.
- RTF text without a color table in `RichTextBox` takes the class text color instead of black.
- ListView and TreeView keep the dark theme when the application sets its own (`SetWindowTheme`); ListView header cells
  left to Windows in owner-drawn lists get the text color.
- PropertyGrid expand glyphs are visible on dark category rows.
- DataGridView: columns with the application's own background keep a readable text color; row errors are shown when
  row headers are hidden; the contrast audit no longer treats FormWind's own grid handlers as owner drawing.
- Button padding leaves room for an image beside the text and for docked `AutoSize` buttons.
- `AutoStyle` follows copied backgrounds when the container got its classes first.
- Very large arbitrary values (`w-[99999999999px]`) and `text-[0px]` are ignored instead of throwing.
- Destroyed native tooltip windows are forgotten, so a reused handle is themed again.
- Adding many controls one by one with `AutoStyle` no longer slows down quadratically.

- Disabled standard `Label` text is redrawn in a readable color when the classes set the text color.
- Disabled standard `CheckBox` and `RadioButton` text is redrawn in a readable color when the classes set the text
  color (it was nearly invisible in dark mode).
- The contrast audit checks the target color of disabled flat buttons during a transition (false reports).

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

[Unreleased]: https://github.com/ModuShip/FormWind/compare/v0.1.1-preview.1...HEAD
[0.1.1-preview.1]: https://github.com/ModuShip/FormWind/compare/v0.1.0-preview.1...v0.1.1-preview.1
[0.1.0-preview.1]: https://github.com/ModuShip/FormWind/releases/tag/v0.1.0-preview.1
