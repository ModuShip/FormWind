# Architecture

This document describes how FormWind is put together: the path a class string takes from `SetClasses` to the screen,
where each concern lives in the code, and the decisions that shape it. It is meant for contributors and for anyone who
needs to maintain a fork. For usage, see the [README](README.en.md). For contribution rules, see
[CONTRIBUTING.md](CONTRIBUTING.md).

## The pipeline at a glance

```
SetClasses(control, "bg-white hover:bg-gray-100 dark:bg-gray-900 rounded-lg px-4")
        │
        ▼
TailwindProvider ── one TwBinding per control (events, state, original values)
        │
        ▼
TwTheme.Parse ── cached ──► TwParser ──► TwParsed (list of TwVariant: conditions + part + TwStyle)
        │
        ▼                        state = Dark | Hover | Focus | Disabled | sm: … (TwState flags)
TwParsed.Resolve(state) ──► merged TwStyle (null = "not touched by the classes")
        │
        ▼
TwAppliers.Find(control) ──► IStyleApplier.Apply(control, style, TwApplyContext)
        │                         (DataGridViewApplier, ToolStripApplier, FormApplier, registered ones,
        │                          WinFormsApplier as the fallback)
        ▼
Properties (BackColor, Font, Padding …), ITwChrome / TwDecorator (corners, borders, gradients),
TwOutset (shadows, focus ring, frames), TwLayout (flex/grid data), TwAnimator (color transitions)
```

Parsing and applying are separate steps. Parsing is pure and depends only on the theme. Applying depends on the
control, its current state and the DPI.

## Main parts

### `TailwindProvider` (`src/FormWind/TailwindProvider.cs`)

This is the entry point. It is an `IExtenderProvider` component, so the Designer shows a `Classes` property on every
control and writes `SetClasses` calls to `Designer.cs`. The provider owns the following:

- **Bindings:** a `Dictionary<Control, TwBinding>`, plus column classes (`TwColumnBinding`) and row rules (`TwRowRule`)
  for DataGridView.
- **Global switches:** `DarkMode`, `RespectHighContrast`, `DarkScrollbars`, `ScaleWithDpi`, the theme, and Designer
  preview.
- **Cross-control behavior:** a hover timer that clears hover state when the cursor leaves through a child control,
  `group-hover`, breakpoint tracking per form (`TwBreakpoints`), DPI changes, validation errors (`SetError`, `invalid:`),
  and error reporting (`StyleError`, `UnknownClasses`).
- **AutoStyle:** with `AutoStyle` on, the provider walks `ContainerControl` (set by the `Site` setter in the Designer),
  subscribes to `ControlAdded` of the containers it visits (forms, panels, groups, tab pages; not composite controls such
  as `TwTextBox`) and gives each control the component mapped to its type (`TwTheme.GetAutoStyle`). A binding keeps the
  control's own classes (`OwnClasses`, what `GetClasses` returns and the Designer serializes) apart from the automatic
  component (`AutomaticClasses`); the applied string is the automatic component followed by the own classes, unless the
  own classes name a component. A theme change re-derives the automatic components.

`ISupportInitialize` defers work until `EndInit`, so a form's `InitializeComponent` does not apply styles one property
at a time.

### `TwBinding` (`src/FormWind/TwBinding.cs`)

There is one binding per styled control. It does the following:

- Subscribes to the control's events (mouse, focus, enabled, checked, RTL, DPI, handle creation, disposal) and computes
  the current `TwState`.
- Captures the control's **original values** (`TwOriginal`) before the first application. When a class is removed or a
  state ends (for example, hover), these values are restored.
- `Refresh()` is the hot path. It returns early when the parsed classes, state and DPI scale are unchanged. It resolves
  the style, builds a `TwApplyContext` and calls the applier.
- It guards against re-entrancy: some property setters recreate the window handle, which raises focus events
  mid-apply. A nested refresh is queued (`_pending`) instead of running inside the current one.
- It re-applies once on the first `HandleCreated`. This undoes the double scaling that form `AutoScale` would otherwise
  apply to values set in `InitializeComponent`.

### Parsing: `TwParser`, `TwParsed`, `TwStyle`, `TwState` (`src/FormWind/Core/`)

- `TwParser` turns a class string into variants. It is a pure function. Results are cached per theme in
  `TwTheme.Parse`, keyed by the normalized string, and the cache is cleared when the theme changes. Component classes
  from the theme (`btn-primary`, `card`) are expanded recursively, up to a fixed depth.
- A **variant** is a set of required states (`TwState` flags), an optional **part**, and a `TwStyle`. For example,
  `dark:hover:` is the `Dark | Hover` state and `header:` is the DataGridView header part. Base classes have no
  conditions.
- `TwState` bit order **is** the precedence order. `Resolve(state)` merges every variant whose conditions are a subset
  of the state, in bit order, so `hover:` overrides `dark:` and `disabled:` overrides `hover:`. High Contrast converts
  colors to system colors, and `forced-colors:` variants are applied last.
- In `TwStyle`, `null` means "not set". Pixel values are stored at 96 DPI and scaled during the apply step.
- Unknown classes do not throw. They are collected in `TwParsed.Unknown` and reported through `UnknownClasses`.

### Theme and presets (`src/FormWind/Core/TwTheme.cs`, `TwPresets.cs`, `src/FormWind/Presets/`)

- `TwTheme` holds the palette, the components, fonts, icons and the AutoStyle map. `DefaultComponents` use the semantic
  colors (`SemanticColors`: `primary`, `surface`, `danger`, `success`, `warning`), which copy palette colors when the
  palette loads and stop following the palette once the application defines them (`SetColor`).
- A preset (`TwPreset`) is a theme file in the `tw.config.json` format, embedded as `FormWind.Presets.<name>.json`. It
  sets the semantic ramps, the fonts and the components whose shape differs; components it does not list are the
  defaults drawn with its colors. `"preset"` in a theme file is applied before the file's other keys.
- Each preset is checked by `PresetTests` (all classes known, contrast audit in light and dark mode, readable disabled
  buttons) and by the `Preset_*` visual tests, which draw the showcase's `PresetsForm`.

### Applying: `IStyleApplier`, `TwAppliers` (`src/FormWind/Apply/`)

- `TwAppliers.Find` returns the most recently registered applier whose `CanApply` matches. The built-in appliers are
  `DataGridViewApplier`, `ToolStripApplier` and `FormApplier`, and `WinFormsApplier` is the fallback. Third-party
  libraries plug in here; `FormWind.Krypton`'s `KryptonApplier` is an example. Libraries that draw with a palette of their
  own can also derive that palette from the theme, as `TwKryptonTheme` does: it resolves the theme's components (`page`,
  `card`, `btn-ghost`, `btn-primary`, `input`, `list`, `table`) for light or dark mode and writes them to a `KryptonCustomPaletteBase`, and
  `Follow` rebuilds it on the provider's `ThemeChanged` and `DarkModeChanged` events.
- `WinFormsApplier` handles color, font, spacing, size, alignment, cursor, visibility, corners and outsets for standard
  controls. **Its key rule:** it touches a property only if the current style sets it, or if the applier changed it
  earlier (tracked in a `ConditionalWeakTable`). Colors the application assigns itself are therefore preserved, which is
  covered in the README section *Colors assigned by application code*.
- `TwFonts` shares `Font` instances (every `Font` owns a GDI handle) and registers fonts bundled with the application.

### Rendering (`src/FormWind/Rendering/`)

WinForms has no anti-aliased corners, shadows or focus rings, so FormWind draws them by one of three routes:

| Route | Used for | How |
| --- | --- | --- |
| `ITwChrome` | `Tw*` controls (`TwButton`, `TwPanel`, `TwTextBox` …) | The applier passes a `TwChrome` (radius, per-side borders, gradient); the control paints itself, anti-aliased (`TwPainter`). |
| `TwDecorator` | Standard controls | A `Region` for the corners, plus painting for borders and gradients (on panels). Corners are not anti-aliased. |
| `TwOutset` | Shadows, focus rings, frames of native text boxes | Drawn on the parent, outside the control's bounds. |

Other files in this folder are thin wrappers around a single Windows feature: DWM title bars and backdrops
(`TwTitleBar`), dark native scrollbars and tooltips (`TwNativeTheme`, `TwNativeToolTips`), High Contrast
(`TwHighContrast`), keyboard and mouse focus tracking (`TwFocusVisible`), RTL mirroring (`TwRtl`), DPI (`TwDpi`),
color transitions (`TwAnimator`) and in-control animations (`TwMotion`). Undocumented Windows APIs (for example,
`DarkMode_Explorer`) fail silently on systems that do not support them.

### Layout (`src/FormWind/Layout/`)

`TwLayoutEngine` implements a subset of CSS flexbox and grid. Containers opt in through `ITwLayoutHost`; `TwPanel` is
the built-in host. The applier stores per-child layout values (`flex-1`, `col-span-2`, `basis-1/2` …) in `TwLayout`,
and the engine reads them. `Dock` and `Anchor` are ignored inside a flex or grid container.

### Controls (`src/FormWind/Controls/`)

`Tw*` controls exist where a native control cannot be styled well: rounded borders on a text box, owner-drawn lists, a
calendar, and so on. They follow these conventions:

- They implement `ITwChrome` and paint with `TwPainter`. Colors for their parts come from part prefixes such as
  `selected:`, `item:hover:` and `menu:`.
- Owner-drawn controls expose UI Automation trees (`TwAccessibility`: `TwListAccessibleObject` and
  `TwItemAccessibleObject`).
- Sizes are computed in 96-DPI units and scaled with `TwDpi.DeviceDpi(this)`.
- User-facing strings come from `TwTexts`.

### DataGridView (`src/FormWind/Grid/`, `Apply/DataGridViewApplier.cs`)

The grid has the richest part model: `header:`, `cell:`, `row:`, `row-header:`, `alt:`, `selected:`, `match:`, `group:`,
`footer:`, column classes, cell types (badge, progress, check, button, sparkline, date, number, link, tags, actions,
expander), row rules, sorting, filtering, search and grouping (`TwSortableBindingList` as `ITwGridSource`), empty and
loading states (`TwGridDecorator`), and virtual rows (`TwVirtualRows<T>`).

- Values are read through `TwGridData`, which takes a bound grid's values from its list so rows stay shared.
- Group headers and row details do not insert rows: they make a row taller (`TwGridDecorator.DesiredHeight`, supplied
  through `RowHeightInfoNeeded` on bound grids) and push its content down with cell padding in `CellFormatting`; the
  header band and the detail area are painted in `RowPostPaint`, detail controls are child windows placed on every paint.
- `TwGridInversion` subclasses the grid window: in dark mode the grid is styled light, renders itself into a bitmap with
  `WM_PRINTCLIENT` and is copied through an invert + hue-rotate color matrix.

### Charts (`src/FormWind/Charts/`)

`TwChart`, `TwDonutChart`, `TwSparkline` and `TwStat` are owner-drawn controls. `TwCharts` holds what they share: the
fixed categorical palette (light and dark steps, never cycled), nice axis scales, compact numbers, bar/marker/sparkline
drawing and the tooltip. `WinFormsApplier` passes `divide-*`, `label:text-*`, `accent-*` and the theme's
`success`/`danger` colors through `ITwChartControl`; `TwChartAccessibleObject` exposes categories and slices as items.
The grid's `TwSparklineColumn` uses the same sparkline drawing.

### Design time (`src/FormWind/Design/`, `src/FormWind.Analyzers/`)

- `TailwindProviderDesigner` adds a FormWind smart tag to every control, including the class builder: one field per
  group (`TwClassCategories`) that edits only its own classes through `TwClassMerge` (conflict-aware merging, like
  tailwind-merge). `ClassesConverter` provides drop-down suggestions. These are plain properties with TypeConverters,
  so they also work in the out-of-process .NET 8+ Designer. `ClassesEditor` is the "…" editor, available only in the
  in-process .NET Framework Designer.
- `TwDesignTheme` gives the provider the theme of the project being designed: it resolves the designed class through
  `ITypeResolutionService` and reads `tw.config.json` next to its assembly, or the copy that `build/FormWind.targets`
  embeds (`FormWind.tw.config.json`).
- Values that classes manage are not serialized, so the Designer preview does not leak into `Designer.cs`.
- Roslyn analyzers ship inside the `FormWind` package: FW001 (`StyleOverrideAnalyzer`) flags code assignments to
  properties that the classes manage, and FW002 (`FontScalingAnalyzer`) flags the .NET Framework font-scaling problem
  with `AutoScaleMode.Font` under PerMonitorV2.

## Error policy

Styling must never crash the host application. `TwBinding.Refresh` and the paint paths catch non-critical exceptions
(`TwErrors.IsCritical` excludes out-of-memory, thread-abort, access-violation and stack-overflow exceptions), report them through
`TailwindProvider.StyleError`, and retry on the next state change.

## Threading and lifetime

Everything runs on the UI thread. Process-wide or thread-wide state is kept to a minimum and is explicit:

- Font cache: `TwFonts`.
- Animation timer and input tracking: `TwAnimator`, `TwFocusVisible`.
- Native tooltip hook: `TwNativeToolTips`.
- Theme: `TwTheme.Current`.

Per-control side data lives in `ConditionalWeakTable`s, so disposed controls are not kept alive. Bindings remove
themselves when their control is disposed.

## Public API and compatibility

The public surface is tracked in `src/FormWind/PublicAPI.*.txt`, and analyzer rules are tracked in
`AnalyzerReleases.*.md`. Helpers are `internal` by default. The intended extension points are `IStyleApplier`,
`TwApplyContext`, `TwStyle`, `ITwChrome`, `ITwLayoutHost` and the theme file. Tests reach internals through
`InternalsVisibleTo`. Versioning rules are in [CONTRIBUTING.md](CONTRIBUTING.md#versioning).

## Tests (`tests/FormWind.Tests/`)

| Kind | Where | Notes |
| --- | --- | --- |
| Unit | `ParserTests`, `ResolveTests`, `LayoutParserTests` … | Pure parsing and resolution; no windows |
| Control behavior | `ApplierTests`, `InteractionTests`, `ControlInputTests` … | Run on an STA thread (`Sta.Run`) in `UiCollection`; input is simulated by invoking `On*` methods |
| Invariants | `InvariantTests` | Random operation sequences; colors must match the resolved style, and originals must return |
| Visual | `Visual/VisualRegressionTests` | PNG baselines per environment (`Baselines/<windows>-<dpi>-<smoothing>/`) |
| Accessibility | `AccessibilityTests`, `UiAutomationTests` | MSAA objects and a real UI Automation client (separate process) |
| Performance | `Performance/` | Handle leaks and timings; opt-in (`FORMWIND_PERF=1`) |

`build.ps1` runs the same steps as CI. CI fails when `FormWind` line coverage drops below the threshold in
`.github/workflows/ci.yml`.

## Repository layout

| Path | Contents |
| --- | --- |
| `src/FormWind/` | The library (`net472`, `net8.0-windows`) |
| `src/FormWind.Analyzers/` | Roslyn analyzers, packed into `FormWind` |
| `src/FormWind.Krypton/` | Krypton Toolkit integration (separate package): `KryptonApplier` (classes → state palettes) and `TwKryptonTheme` (a Krypton palette built from the theme) |
| `templates/` | `dotnet new formwind-app` |
| `tools/FormWind.Convert/` | Designer-file converter (hard-coded styles → classes) |
| `tools/FormWind.Harness/` | Startup hook for trying FormWind on existing applications |
| `tools/FormWind.Docs/` | Generates the class reference site with rendered previews |
| `website/` | Landing page and documentation in English (`en/`) and Turkish (`tr/`), static HTML; `website/build.ps1` builds it with the class reference |
| `samples/` | Showcase sample application (`FormWind.Showcase`) and a Krypton Toolkit sample (`FormWind.Krypton.Showcase`) |
