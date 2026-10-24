# Changelog

All notable changes to FormWind are documented in this file. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](CONTRIBUTING.md#versioning).

## [Unreleased]

### Added

- `ease-linear`, `ease-in`, `ease-out` and `ease-in-out` pick the curve of a transition, and `delay-*` (`delay-75` …
  `delay-1000`, `delay-[40ms]`) waits before it starts. The default stays ease-out with no delay.
- `transition-shadow` animates only the shadow and ring. `transition-colors` now also animates the border color, and
  `transition` (`transition-all`) also fades the ring in and out (`ring-0 focus:ring-2 transition`).
- `motion-safe:` and `motion-reduce:` variants apply while animations are on or off (`TwMotion.Enabled`), for example
  `animate-spin motion-reduce:animate-none`. Controls that use them are reapplied when the setting changes.
- `animate-spin` turns a `TwIcon` (one turn per second, for loading indicators) and `animate-pulse` fades a `TwIcon` or
  the background of a `TwPanel` (skeleton placeholders). `animate-none` stops them. They stand still when animations
  are off.
- The `TwSwitch` knob, the `TwTabStrip` indicator and the `TwDataList` row hover follow `duration-*`, `delay-*` and
  `ease-*` on the control; `transition-none` makes them immediate.
- `TwDialog` and `TwPopover` fade in when they open and fade out when they close (150 ms). The window closes at once
  (`DialogResult`, `IsOpen` and `Closed` are not delayed); its last picture fades out in a click-through window.
- A new `TwProgress.Value` slides the fill to its place (300 ms; `duration-*`, `ease-*`, `transition-none`).
- `TwStat` counts a new value up from the previous one, or from zero when it first appears (800 ms), keeping the text's
  form (`₺12.400`, `98,4%`); `AnimateValue = false` turns it off. Only a value with a single number is counted.
- `TwChart`, `TwRingChart`, `TwHeatmap`, `TwTimeline` and `TwSparkline` open the first time they are shown with data
  (600 ms per item, 35 ms apart, the delays adding up to 400 ms at most): bars grow from the baseline, lines and areas
  draw from left to right, rings fill from zero, heatmap cells fade in diagonally and timeline dots pop up with a spring
  while the joining line extends. `AnimateOnShow = false` turns it off and `AnimateIn()` plays it again, for example
  after loading new data.
- After a chart was shown, a data change moves the bars, points, ring fills, cell shades and dots on screen to the new
  data in 350 ms instead of redrawing at once; calls that clear and re-add, such as `SetData`, start from what is on
  screen too. `AnimateChanges = false` turns it off. A sparkline with a different number of values is drawn at once.
- `TwPanel.Expanded` (`Expand()`, `Collapse()`, `ExpandedChanged`) slides the panel's height to zero and hides it, or
  shows it and slides it back to its height (its preferred height when `AutoSize`), in 200 ms (`duration-*`, `ease-*`).
  The children keep their places meanwhile.
- `TwTween` is public: progress of a value over time with a curve and a delay, for custom controls; it finishes
  immediately when animations are off. `TwMotion` adds `Changed`, `FollowSystem()`, `CanAnimate`, `Ease`, `Lerp` and
  `Blend`.
- `TwEasing.Spring`: a damped spring that goes about 10% past the target and settles back. `TwCubicBezier` is a custom
  curve like CSS `cubic-bezier(x1, y1, x2, y2)`, for `TwTween.Start` and `TwAnimator.Animate`. `TwMotion.Blend` and
  color transitions clamp colors when a curve overshoots.
- `TwMotion.Durations` gives standard durations: `Xs` 120, `S` 180, `M` 250 and `L` 450 ms, or 1 ms each when
  animations are off.
- `TwAnimator` is public and animates a number of a control: `Animate(control, name, from, to, durationMs, set)`, with
  overloads for the curve, a delay and a `completed` callback, and `Cancel` / `IsAnimating`. A new animation with the
  same name replaces the running one; when the control cannot animate the end value is set at once.
- `TwToast.Position` (`TwToastPosition`: the four corners and the top and bottom centers), `TwToast.MaxVisible` (1 =
  each notification replaces the previous) and `TwToast.EnterEasing`. Notifications slide in from their edge while they
  fade in, a success notification draws its check mark, and `TwToastForm.Subtitle` adds a muted line under the message.
- `TwButton.IsBusy` draws a spinner before the text and ignores clicks, Enter, Space and `PerformClick` (no `Click`, no
  `DialogResult`); screen readers get the busy state.
- Tree rows with columns in a DataGridView: `TwGrid.SetTree(grid, treeColumn, roots)` (or data objects with a children
  selector) shows hierarchical rows indented under their parent with an expand arrow in the tree column. ←/→ in the
  tree column, `+`/`−`/`*`, a click on the arrow or a double-click expand and collapse; header clicks sort within each
  parent; `TwGridTree.SetTotal` shows the children's sum, average, count, minimum or maximum in parent rows, and a
  `TwGridFooter` counts only the rows without children.
- `TwChip`: a pill with a text, an optional icon or status dot and an optional ✕ (`Removable`, `RemoveClick`, also
  Delete/Backspace); `Clickable` and `Checkable` (a filter chip styled with `checked:`). The theme has the `chip`,
  `chip-primary`, `chip-success`, `chip-warning` and `chip-danger` components, and AutoStyle maps `TwChip` to `chip`.
- `TwListItem`: `TwListBox` draws these items with a built-in row template (a status dot, an icon or a small score
  ring, a title and a muted line, a value and a badge styled by classes on the right); two-line rows get taller on their
  own and screen readers read every part.
- `TwToolbar`: a responsive toolbar whose items that do not fit move to a "more" (…) menu that runs them (buttons are
  clicked, check boxes toggled, tab strips and selects become submenus), with per-item alignment, priority and
  overflow mode; a `Shrink` item (a title) gets narrower first.
- `TwSettingRow`: a System Settings style row with a label, a description, the editor and a unit; stacked rows draw
  dividers between them and narrow rows put the editor under the text (`setting` component, `label:` and `hint:` parts).
- `TwDialog.Prompt` and `TwDialogOptions.ShowInput` (`InputText`, `InputLabel`, `InputPlaceholder`, `InputRequired`,
  `InputMaxLength`, `InputValidator`) ask for a line of text; OK validates it and keeps the dialog open with the error
  under the box.
- `TwChartKind.HorizontalBar`: horizontal bar charts with one row per category, the full label on the left, the value
  axis along the bottom and values at the bar ends; side by side or stacked, with vertical reference lines.
- `TwTabStrip.OverflowMenu` (on by default): tabs that do not fit go to a "more" button at the end whose menu selects
  them (↓ opens it, `menu:`/`item:` classes); the selected tab stays visible. `TwTabStrip.UseMnemonic` (on by
  default, as on a Label) and Alt shortcuts for tabs.
- `TwHeatmap.EmptyCells` (`Low`, `Solid`, `Hatched`, `None`), `EmptyCellColor` and `EmptyCellText`: missing values can
  be drawn as flat or hatched gray, named ("No production") in the tooltip and for screen readers, and keyed in the
  legend.
- `TwStat.ShortValue` and `TwStat.ValueFit`: a value that does not fit switches to its short form and then gets
  smaller, down to the title's size.
- `backdrop-blur-sm` … `backdrop-blur-3xl` (`TwPanel.BackdropBlur`): a frosted `TwPanel` that blurs what the parent
  paints behind it, under a translucent `bg-*/NN`.
- `TwTexts.Remove` and `TwTexts.More` (screen reader names of the chip's ✕ and the "more" buttons).

### Changed

- A `TwPanel` with a translucent background (`bg-white/70`) shows the parent's painting (a gradient or an image)
  through. Before, it was blended over the parent's background color only.
- `TwStat` shrinks a value that does not fit instead of cutting it with "…" (`ValueFit = Ellipsis` restores that).
- A `TwRingChart` as small as an icon keeps a ring at least 3 px thick and shrinks its center text to fit.
- The `tabs` component colors the overflow menu (`menu:bg-* menu:border-*`, with `dark:` variants).
- `transition-colors` no longer animates shadows, as in Tailwind; use `transition` (or `transition-shadow`) for them.
- `TwToast` fades in and out in 150 ms whatever the frame rate. Before, each frame added a fixed step, so on a busy UI
  thread the fade took longer.
- `TwMotion.Enabled` notices when the Windows "Show animations" setting changes while the application runs.

### Fixed

- `TwTabStrip` no longer clips the tabs that do not fit (they go to the overflow menu), and screen readers read tab
  names without the `&` shortcut marker.
- `transition`, `TwSwitch`, `TwToast` and indeterminate `TwProgress` follow `TwMotion.Enabled` (Windows' "Show
  animations" setting). Before, they animated even when animations were off. Now color and shadow transitions and the
  switch knob change immediately, toasts appear and close without fading, and the indeterminate progress segment
  stands still in the middle of the track.
- The `net472` project of the `formwind-app` template comes with an App.config that sets `DpiAwareness` to
  `PerMonitorV2`. Before, PerMonitorV2 came only from the manifest, which leaves WinForms' per-monitor support off: a
  form moved to a screen with another scale kept the layout and fonts of the old DPI.

### Changed

- README: "Setup on .NET Framework" under DPI: the DPI-unaware, system-aware and PerMonitorV2 options, why
  PerMonitorV2 needs both the manifest and App.config, why App.config must not declare the section in `configSections`,
  and how to move an application with its own DPI factor (`Px()`-style helpers) to DPI-aware without scaling twice.

## [0.1.2-preview.1] - 2026-10-21

### Added

- `TwTheme.SuggestClass` returns the closest recognized class for an unknown one, variant prefixes included
  (`hovr:bg-blu-600` → `hover:bg-blue-600`). The Debug output for unknown classes, the class editor and the smart tag
  show the suggestions.
- `formwind-mcp` (`FormWind.Mcp` dotnet tool): an MCP server for coding agents with the tools `validate_classes`,
  `explain_classes`, `list_components`, `list_vocabulary` and `render` (light and dark PNGs per state), and `llms.txt`
  and the presets as resources. Read-only; runs no application code.
- `TwPopover`: a panel that opens next to a control and closes on a click outside or Esc. It shows any control as
  `Content` on the `Placement` side of the anchor (or of an area of it, such as a grid cell), flips to the opposite side
  and stays inside the screen when it does not fit, focuses the content and returns the focus to the anchor
  (`RestoreFocus`). `Toggle` does not reopen a popover that the same click on the anchor just closed.
- `website/llms.txt`: a summary of the classes, components, variants and rules for coding agents, served at the site
  root and checked against the parser by tests.

### Changed

- `TwDatePicker` opens its calendar in a `TwPopover`: it flips above the field near the bottom of the screen, follows
  right-to-left layout, and a click on the calendar icon while the calendar is open closes it instead of reopening it.

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

[Unreleased]: https://github.com/ModuShip/FormWind/compare/v0.1.2-preview.1...HEAD
[0.1.2-preview.1]: https://github.com/ModuShip/FormWind/compare/v0.1.1-preview.1...v0.1.2-preview.1
[0.1.1-preview.1]: https://github.com/ModuShip/FormWind/compare/v0.1.0-preview.1...v0.1.1-preview.1
[0.1.0-preview.1]: https://github.com/ModuShip/FormWind/releases/tag/v0.1.0-preview.1
