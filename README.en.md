# FormWind

[Türkçe](README.md) | **English**

**Modernize your existing WinForms application without rewriting it:** a Tailwind-style styling layer that works with the standard controls and with Krypton Toolkit. Controls stay as they are; picking a preset (Fluent, Ant Design, Material) and turning on `AutoStyle` styles a form without writing classes, and classes can be added per control where needed.

Drop a `TailwindProvider` on a form and every control gets a `Classes` field in the Properties window:

```
btnSave  →  Classes: "bg-blue-600 text-white px-4 py-2 rounded-md font-bold hover:bg-blue-700"
```

- Targets: .NET Framework 4.7.2+ and .NET 8+ (`net472`, `net8.0-windows`); tests also run on .NET 9 and 10
- The core package has no dependencies beyond .NET (only `System.Text.Json` on `net472`)
- State variants: `hover:`, `focus:`, `active:`, `disabled:`, `checked:`, `dark:` and combinations (`dark:hover:`)
- Flex and grid layout (`flex`, `gap-4`, `justify-between`, `flex-1`, `w-1/2`, `grid-cols-3`, `col-span-2`)
- Component classes (`btn-primary`, `card`) and a JSON theme file
- Presets (Tailwind, Fluent, Ant Design, Material) and `AutoStyle`, which styles forms without writing classes
- `TwButton`, `TwPanel`, `TwTextBox` with anti-aliased rounded corners
- Native scrollbars and tooltips also turn dark in dark mode
- WCAG contrast audit (`TwContrast`) and Windows High Contrast support
- Krypton Toolkit integration (`FormWind.Krypton`): classes on Krypton controls and a Krypton palette that follows the theme
- A harness for trying FormWind on real applications without touching their source (`tools/run-harness.ps1`)

| Light | Dark |
| --- | --- |
| ![Light](docs/showcase-light.png) | ![Dark](docs/showcase-dark.png) |

## When is FormWind a fit?

| Situation | Suggestion |
| --- | --- |
| Modernizing an existing WinForms application without rewriting it | FormWind: standard controls stay as they are; start without classes with `AutoStyle` and a preset, add classes per control where needed. |
| An application that uses Krypton Toolkit | FormWind + `FormWind.Krypton`: Krypton's ribbon, docking and navigator stay in place, and its palette follows the theme. |
| Dark mode, consistent colors, accessibility (contrast, High Contrast, UI Automation) | FormWind. |
| Heavy components such as ribbon, docking, scheduler, reporting, advanced grids | Take them from Krypton or the control suite you use; FormWind styles the standard controls next to them. |
| A brand-new, cross-platform application | Avalonia, WPF or a web-based UI may suit better than WinForms. |

## Quick start

1. Add the package (`dotnet add package FormWind --prerelease` while FormWind is in preview) or a project reference to `src/FormWind`.
2. In `Program.Main`, load the theme before any form is opened (the default theme is used if the file does not exist):

   ```csharp
   TwTheme.Current = TwTheme.LoadIfExists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tw.config.json"));
   ```

   The package copies `tw.config.json` from the project folder to the output and publish folders (in a single-file publish it stays next to the exe; `<FormWindCopyTheme>false</FormWindCopyTheme>` turns this off). In .NET Framework applications the package turns on binding redirects (`AutoGenerateBindingRedirects`) itself, in old-style projects too. If the application takes its `app.config` from somewhere else (KeePass: `..\Ext\KeePass.exe.config`), the build reports it with warning `FW100`; point `<AppConfig>` at that file, otherwise the redirects end up in a new file without the application's settings. If you repack the package locally under the same version, NuGet uses the old copy in its cache; bump the version for every try.

3. Drag `TailwindProvider` from the Toolbox onto the form. If it is missing: Toolbox → right-click → **Choose Items** → `FormWind.dll`.
4. Select controls and fill in `Classes on tailwindProvider1`. The Designer generates:

   ```csharp
   this.tailwindProvider1.SetClasses(this.btnSave, "btn-primary");
   ```

5. Dark mode: `tailwindProvider1.DarkMode = true;`

### An existing application: one line

To style a whole application without changing its forms one by one, in `Program.Main` before the first form is created:

```csharp
Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
TwAutoTheme.Install();                 // loads tw.config.json and gives every form that opens a provider with AutoStyle on
TwAutoTheme.DarkMode = true;           // every form; or TwAutoTheme.FollowWindowsTheme = true
```

- A form is styled after its `Load` event and before it appears (`WM_SHOWWINDOW`), so it is never seen unstyled. The provider is disposed with the form.
- Forms that have their own provider (a field of type `TailwindProvider`, e.g. one added in the Designer) are left to it. Only top-level forms get a provider; MDI children and forms embedded as controls are styled by their parent's provider.
- Forms opened on other UI threads are found by a background check; the first form on such a thread may appear briefly before it is styled, later ones are styled before they appear.
- `TwAutoTheme.Filter = f => f is not LogForm;` leaves forms out. Give single controls their own classes in the `TwAutoTheme.FormAttached` event (`e.Provider.SetClasses(…)`). `TwAutoTheme.GetProvider(form)` returns a form's provider; `Uninstall()` gives the original look back.
- Run the converter's `--audit` scan first (see Tools): it lists the places to look at by hand, such as colors assigned in code and controls that draw themselves.
- On KeePass 2.61.1: 1 line instead of a hand-written 44-line helper class and a call added to its window manager. With a dark mode menu item the integration is 17 lines; the only manual fix left is two `ResetBackColor()` calls for the group tree.

### Together with WPF

FormWind also works with WinForms controls in WPF applications (tried with net472 and .NET 8+, `WpfInteropTests`):

- **WinForms in WPF (`WindowsFormsHost`):** the provider's `ContainerControl` can be a `UserControl` (no form needed); AutoStyle, `FollowParentBackColor`, dark mode and the contrast audit work. When the WPF window moves to a monitor with another DPI, the hosted controls are restyled at the new scale; tabbing between WinForms controls shows keyboard focus (focus-visible) and focus cues correctly.
- **WPF in WinForms (`ElementHost`):** FormWind gives `ElementHost` no component and leaves its WPF content alone; if it copied the form's background, it follows the page background. The contrast audit does not treat `ElementHost` as a control that carries text (it cannot see the WPF text inside). The colors of the WPF content are WPF's business.
- **WinForms forms opened from WPF:** `TwAutoTheme.Install()` styles modal (`ShowDialog`) and modeless (`Show`) forms before they appear; the hook does not depend on the message loop. Color transitions, hover tracking and system setting changes (high contrast, text size, theme) work on WPF's Dispatcher too. For the keyboard (Tab, shortcuts) in a modeless WinForms form call `WindowsFormsHost.EnableWindowsFormsInterop()`, which WinForms itself requires.
- **Setup:** a WPF application does not turn on WinForms visual styles; call `System.Windows.Forms.Application.EnableVisualStyles()` before creating WinForms controls. Otherwise ListView groups and themed headers are not drawn, and FormWind's dark list header (`DarkMode_ItemsView`) does not work either.
- **Dark mode bridge:** the WPF side switches its own colors; the single switch for the WinForms side is `TwAutoTheme.DarkMode`. Providers of hosted controls follow it:

  ```csharp
  void SetDark(bool dark)
  {
      Background = dark ? darkBrush : lightBrush;   // WPF side
      TwAutoTheme.DarkMode = dark;                  // WinForms forms opened from WPF
  }
  // provider of the UserControl inside WindowsFormsHost:
  provider.DarkMode = TwAutoTheme.DarkMode;
  TwAutoTheme.DarkModeChanged += (_, _) => provider.DarkMode = TwAutoTheme.DarkMode;
  ```

  To follow Windows' light/dark setting, set `TwAutoTheme.FollowWindowsTheme = true`.
- In WPF applications WinForms controls draw with GDI+ (`UseCompatibleTextRendering`, as `SetCompatibleTextRenderingDefault` is not called); disabled text that FormWind redraws is measured with GDI+ too. Before, the text of a disabled check box broke onto two lines at 125 %.

### Project template

The `FormWind.Templates` package creates an app with ready-made screens: sign-in (validated fields), list-detail (searchable `TwDataList` with a detail card) and settings; the light/dark switch is shared across forms.

```bash
dotnet new install FormWind.Templates
```

```bash
dotnet new formwind-app -n MyApp --Framework net8.0-windows --Preset fluent --Krypton true
```

`--Framework`: `net8.0-windows` (default), `net9.0-windows`, `net10.0-windows`, `net472`; `--Preset`: `tailwind` (default), `fluent`, `antd`, `material` (see [Presets](#presets-and-autostyle)); `--Palette`: `v3` (default), `v4`; `--Krypton true`: adds the `FormWind.Krypton` package, registers `KryptonApplier` and binds the Krypton palette to the main form's theme (see [Krypton Toolkit](#krypton-toolkit)); `--FormWindVersion`: package version (default: the same version as the template package). For a preview, give the version explicitly when installing: `dotnet new install FormWind.Templates::0.1.2-preview.1`. Forms are built in code without the Designer and use `AutoScaleMode.Dpi`; the `net472` project comes with a manifest and App.config for PerMonitorV2 (see [Setup on .NET Framework](#setup-on-net-framework)). CI generates and builds the template in three variants on every run (default; `net472` with `v4`; `--Krypton true` with `material`).

## Presets and AutoStyle

To start without writing classes, pick a preset and turn on `AutoStyle`:

```jsonc
// tw.config.json
{ "preset": "fluent" }   // tailwind (default), fluent, antd, material
```

```csharp
tailwindProvider1.AutoStyle = true;   // in the Designer: Properties → FormWind → AutoStyle
```

| Tailwind | Fluent | Ant Design | Material |
| --- | --- | --- | --- |
| ![Tailwind](docs/preset-tailwind.png) | ![Fluent](docs/preset-fluent.png) | ![Ant Design](docs/preset-antd.png) | ![Material](docs/preset-material.png) |

**Presets** (`TwPreset`; in code `new TwTheme(TwPreset.Fluent)`) set the colors, fonts and the shape of the components (corners, spacing, weight). The looks are **inspired by** the named design systems; they are not those systems and contain no logos or assets. Where the original fails WCAG AA contrast, the color is darkened: Ant Design's `#1677ff` blue gives 4.1:1 with white text, so filled buttons use `primary-600` (`#0958d9`). Fluent is drawn with Segoe UI Variable on Windows 11, Material with Roboto when it is installed; otherwise both use Segoe UI. Every preset passes the contrast audit and the visual regression tests in light and dark mode.

All presets define **the same component names**, so switching presets restyles the whole application without touching its classes or Designer.cs:

| Group | Components |
| --- | --- |
| Buttons | `btn`, `btn-primary`, `btn-secondary` (filled neutral), `btn-ghost` (outlined default), `btn-danger`, `link` |
| Forms | `input`, `select` (input and drop-down), `field`, `setting` (`TwSettingRow`), `toggle`, `range` (slider, progress) |
| Surfaces and text | `page` (form background, text color and font), `card`, `sidebar`, `nav-item`, `title`, `muted`, `badge`, `chip`, `chip-primary`, `chip-success`, `chip-warning`, `chip-danger`, `alert-info`, `alert-success`, `alert-warning`, `alert-danger` |
| Data and navigation | `table`, `list`, `tabs`, `tab-control`, `tab-page`, `menu`, `properties` |

**Semantic colors.** The components use the colors `primary`, `surface` (neutral backgrounds and text), `danger`, `success` and `warning` (`bg-primary-600`, `text-surface-500` …). In the default theme they copy `blue`, `gray`, `red`, `green` and `amber` and follow the palette version. Defining a ramp in the theme file (`"colors": { "primary": { "50": "…", …, "950": "…" } }`) recolors every component at once. Classes of your own that use the semantic names change with the preset too.

**Customizing a preset.** The theme file is applied on top of the preset; write only what differs (for example your own `primary` ramp or `btn` component next to `"preset": "antd"`). To change a preset throughout, copy its file from [`src/FormWind/Presets/`](src/FormWind/Presets) as `tw.config.json`; the files use the same format.

**AutoStyle.** While `TailwindProvider.AutoStyle` is on, the controls of the `ContainerControl` form get the component mapped to their type, including controls added later. In the Designer, `ContainerControl` is set to the form itself (as with ErrorProvider); in forms built in code write `provider.ContainerControl = this;`.

| Control | Component |
| --- | --- |
| `Form` | `page` |
| `Button`, `TwButton` | `btn-ghost` |
| `TextBox`, `RichTextBox`, `MaskedTextBox`, `TwTextBox`, `TwNumberBox`, `NumericUpDown`, `DomainUpDown` | `input` |
| `ComboBox`, `TwSelect`, `TwDatePicker`, `DateTimePicker` | `select` |
| `CheckBox`, `RadioButton`, `TwCheckBox`, `TwRadioButton`, `TwSwitch` | `toggle` |
| `TwSlider`, `TwProgress`, `ProgressBar` | `range` |
| `TwField` | `field` |
| `DataGridView` | `table` |
| `ListBox`, `ListView`, `TreeView`, `TwDataList`, `TwListBox`, `TwTreeView` | `list` |
| `TwTabStrip` (and the strip of `TwTabControl`) | `tabs` |
| `TwChip` | `chip` |
| `TwSettingRow` | `setting` |
| `MenuStrip`, `ToolStrip`, `StatusStrip` | `menu` |
| `LinkLabel` | `link-label` |
| `TabControl`, `TabPage` | `tab-control`, `tab-page` |
| `PropertyGrid` | `properties` |

- Labels and panels are not listed: they take the form's text and background colors.
- **Copied backgrounds.** Theme engines of existing applications often write the form's background into every control explicitly. When FormWind repaints the form, those controls keep the old background and labels that only get a text color become unreadable (dark text on a dark background). On its first walk of a container, `AutoStyle` leaves controls whose background equals their parent's to follow the parent (`FollowParentBackColor`, on by default; not done in the Designer). Controls that would fall back to a different default when reset (e.g. TextBox) keep their color; controls added later are compared with the parent's original background. If you assign classes yourself, `TailwindProvider.InheritCopiedBackColors(form)` does the same before styling.
- A control's own classes are added **on top of** the automatic component: `flex-1`, `mt-2` or `bg-red-50` keep the look. When its own classes name a component (`btn-primary`, `card` …), the automatic one is not used.
- Automatic classes are not written to Designer.cs; the smart tag shows them as `AutoStyle: input`, and the class builder writes on top of them.
- The mapping is changed in the theme file: `"autoStyle": { "Button": "btn-primary", "Label": "muted", "KryptonButton": "btn-ghost", "ListView": "" }`. The key is the short type name and base types are searched too (`TextBoxBase` covers all text boxes); an empty value leaves that type unstyled. In code: `theme.SetAutoStyle("Button", "btn-primary")`.
- `LinkLabel` gets the `link-label` component: the text outside the links is `text-*`, the links (`LinkColor`, `ActiveLinkColor`, `VisitedLinkColor`) `accent-*`; a disabled link is the text color mixed halfway toward the background. The contrast audit checks the link color as the `link` part.
- Composite controls with inner controls (the text box of `TwTextBox`, `NumericUpDown`, `PropertyGrid`) are not entered. `PropertyGrid` is styled through its own color properties: `bg-*`/`text-*` the grid view, help and commands panes, `divide-*` the lines between them, `header:` category rows, `selected:` the selected property; in dark mode the inner scrollbar is darkened too, and on dark category rows the expand glyphs are drawn as +/− in the text color.
- The standard `TabControl` ignores colors, so with classes that give colors FormWind repaints the tab strip over Windows' drawing (`DrawMode` is not changed; changing it would recreate the handles of every control on the pages): `bg-*`/`text-*` the tabs, `selected:` the selected tab, `selected:border-*` a 2 px bar on its outer edge; the empty part of the strip takes the parent's background and the frame around the pages the selected page's. Tabs the application owner-draws itself (`OwnerDrawFixed`) are left alone.
- The standard `ProgressBar` takes no colors while themed; with `accent-*` (bar) or `fill-*`/`bg-*` (track) its visual style is turned off and the colors are sent to the native control (a flat, classic bar; the `Blocks` style becomes `Continuous` meanwhile). Removing the classes restores the theme and the style.

The showcase has a form that switches between the presets: `FormWind.Showcase.exe --presets` (`--preset fluent` for the initial preset).

## Class reference

| Category | Pattern | Example | WinForms equivalent |
| --- | --- | --- | --- |
| Background | `bg-{color}-{shade}`, `bg-[#hex]` | `bg-blue-600`, `bg-white` | `BackColor` |
| Text color | `text-{color}-{shade}` | `text-gray-900` | `ForeColor` |
| Font size | `text-xs … text-4xl` | `text-sm` = 14 px | `Font` (px × 0.75 = pt) |
| Font style | `font-thin` … `font-black`, `font-sans`, `font-mono`, `italic`, `underline` | `font-semibold` | `Font` (see [Font weights](#font-weights)) |
| Padding | `p`, `px`, `py`, `pt`, `pr`, `pb`, `pl` + `-{n}` / `-px` / `-[12px]` | `px-4` = 16 px | `Padding` |
| Margin | `m`, `mx`, `my`, `mt`, `mr`, `mb`, `ml` + `-{n}` | `mt-2` = 8 px | `Margin` (in layout panels) |
| Size | `w-{n}`, `h-{n}` | `h-10` = 40 px | `Width`, `Height` |
| Border | `border`, `border-{0,2,4,8}`, `border-{color}-{shade}` | `border border-gray-300` | Painted |
| Corners | `rounded`, `rounded-{none,sm,md,lg,xl,2xl,3xl,full}`, `rounded-[10px]` | `rounded-md` = 6 px | `Region` or owner-draw |
| Alignment | `text-left`, `text-center`, `text-right` | | `TextAlign` |
| Other | `cursor-pointer`, `cursor-text`, `hidden`, `visible`, `truncate` | | `Cursor`, `Visible`, `AutoEllipsis` |

- The spacing unit is 4 px (configurable with `spacingUnit`). Numbers always use a dot, in every culture: `p-0.5`.
- Palette: the 22 colors × 11 shades (50–950) of Tailwind v3, plus `white`, `black`, `transparent`. The Tailwind v4 palette can be selected too (see [Theme file](#theme-file-twconfigjson)).
- Within the same variant the later class wins: `p-2 px-4` → 8 px vertical, 16 px horizontal.
- Unknown classes are reported through the `TailwindProvider.UnknownClasses` event (or the Debug output when nobody listens). `Theme.SuggestClass("hovr:bg-blu-600")` returns the closest recognized class (`hover:bg-blue-600`); the Debug output, the class editor and the smart tag show these suggestions.
- [`website/llms.txt`](website/llms.txt) summarizes the classes, components and rules for coding agents (served at the site root).
- Malformed classes do not crash the application: arbitrary values that do not fit an `int` (`w-[99999999999px]`) and a zero font size (`text-[0px]`) are unknown classes. Theme components that contain themselves repeatedly or form a cycle are expanded once (duplicates are removed, the later class wins); the name that closes a cycle is reported as an unknown class. Errors in appliers are reported through `StyleError` and do not escape (`BadInputTests`).
- In CSS, padding grows the box; in WinForms, on a fixed-size (`AutoSize` off) button or label it shrinks the content. On these controls padding is reduced just enough for the text to fit on one line (e.g. `py-2` shrinks when `btn` is applied to a 25 px tall button). On `AutoSize` controls that are docked, the parent sets the size along the docked axis, so padding is reduced on that axis too (e.g. the height of an AutoSize button with `Dock = Right`; in Bulk Crap Uninstaller's wizard the text was cut off at the bottom). It is recalculated when the control grows or its font or text changes, and returns to the full value when there is room. A button's image (`Image`, `ImageList`) beside or above the text gets its room too; an image assigned while the form loads is measured again when the form becomes visible (the text of KeePass's "Tools" button with an image was cut to "To").
- Padding fitting cannot shrink the font: if a fixed-size control gets a class that enlarges its font (e.g. `text-sm font-semibold` in `btn`), the text may not fit even with zero padding; do not give such controls a font class, or make the control larger.
- Removing `hidden` makes a control hidden by FormWind visible again.
- **MDI:** the `bg-*` color of an `IsMdiContainer` form is also given to the area behind the child windows (`MdiClient`); otherwise that area keeps the system gray and becomes a light gap in dark mode. A color the application assigned to the `MdiClient` is kept. The minimize/restore/close buttons that a maximized child adds to the menu are drawn in the strip's text color. Windows does not draw dark title bars on MDI children (classic frame), and the `MdiClient`'s sunken edge shows as a light line in dark mode; both are outside FormWind's control.
- **Boxes hosted in a strip:** `ToolStripTextBox` and `ToolStripComboBox` are not drawn by the strip's renderer and stayed white on a dark strip. They take the strip's text color and a background derived from the strip (white on a light strip, slightly lighter than the background on a dark one); the combo box's frame and arrow button are drawn with these colors too. Boxes in sub-menus that share the strip's renderer and boxes added later are included. Colors the application assigned to these boxes are kept.
- **Disabled native text boxes:** `TextBox` and `MaskedTextBox` keep the classes' background while disabled; Windows follows the background and always draws the text in the system gray (#6D6D6D) (measured with the color set before and after disabling, and after toggling). `RichTextBox`, however, ignores the background while disabled and paints the system background (`EM_SETBKGNDCOLOR` has no effect either) but honors the text color; since that produced light text on a light background in dark mode, while disabled it only takes the colors of `disabled:` variants, otherwise Windows' look (gray text on a light background) remains. Controls FormWind draws itself (including `TwTextBox`) are not affected.
- **Disabled buttons:** Windows draws the text of a disabled flat button in a darkened shade of its background instead of `ForeColor`, which made the text invisible on a dark background. On buttons styled by FormWind the text of a disabled button is redrawn with the `disabled:text-*` color, or, without one, with the text color mixed halfway toward the background (`TwButton` follows the same rule). Native buttons with an image keep Windows' drawing. The default `btn-primary`, `btn-ghost` and `btn-danger` components define `disabled:` colors for light and dark mode; `disabled:` takes precedence over `dark:`, so give your own components `dark:disabled:` colors for dark mode as well.
- A class color is assigned explicitly even when it equals the inherited parent color; it does not change when the parent's color changes later.
- When classes are removed (`SetClasses(control, "")`), color, font, padding, margin, cursor, visibility, corners and the ToolStrip renderer return to their values from before the classes. Sizes (`w-*`, `h-*`) are not reverted, because the layout may have resized the control since.

### Colors assigned by application code

If application code changes a control's `BackColor` or `ForeColor` after classes have been applied (e.g. painting a status indicator green), FormWind leaves that color channel to the application: `hover:`, `dark:` and other variants do not override it. The other channel stays with FormWind.

```csharp
tw.SetClasses(lblStatus, "bg-white text-gray-700 dark:bg-gray-800 dark:text-gray-200");
lblStatus.BackColor = Color.LightGreen;   // stays green in dark mode too; the text color comes from the classes
tw.SetClasses(lblStatus, "bg-red-600 text-white");   // new classes: the background belongs to FormWind again
```

- Color changes inherited from the parent (e.g. the form switched to dark mode) do not count as application assignments.
- To give the channel back, assign different classes; `Refresh(control)` is not enough.
- If the application changes only the background and leaves the text color to the classes, dark mode may produce an unreadable pair (light text on a light background). The [contrast audit](#contrast-audit) reports this and gives the cause as `Cause = TwContrastCause.KeptBackground`.

**Code that paints controls itself.** Where the application paints the colors (a `CellPainting` handler, an owner-drawn ListView), ask the provider for the class colors; the result follows the provider's current mode (dark, high contrast):

```csharp
var selected = tw.GetColors("list", "selected");            // components and parts are expanded
var hover = tw.GetColors("list", "item", TwState.Hover);
var own = tw.GetCurrentColors(lvUploads, "selected");          // the control's classes and current state
e.Graphics.FillRectangle(new SolidBrush(selected.Back ?? SystemColors.Highlight), e.Bounds);
```

## Layout (flex and grid)

A **`TwPanel`** with the `flex` or `grid` class lays out its children itself; the children's `Location`, `Dock` and `Anchor` are ignored. Because the layout engine of the standard `Panel` and `Form` cannot be replaced, flex/grid only works on `TwPanel` (or a container implementing `ITwLayoutHost`).

```csharp
// Card: vertical stack, items fill the width
this.tailwindProvider1.SetClasses(this.pnlCard, "card flex flex-col gap-1");
// Button row: right-aligned, 8 px apart
this.tailwindProvider1.SetClasses(this.pnlActions, "flex justify-end gap-2");
// Spacer: takes the remaining space and pushes the button row to the bottom
this.tailwindProvider1.SetClasses(this.pnlSpacer, "flex-1");
```

| Container (TwPanel) | Item (child control) |
| --- | --- |
| `flex`, `grid`, `block` | `flex-1`, `flex-auto`, `flex-initial`, `flex-none` |
| `flex-row`, `flex-col`, `flex-wrap`, `flex-nowrap` | `grow`, `grow-0`, `shrink`, `shrink-0` |
| `gap-{n}`, `gap-x-{n}`, `gap-y-{n}` | `basis-{n}`, `basis-1/2`, `basis-full`, `basis-auto` |
| `justify-start/end/center/between/around/evenly` | `w-1/2`, `w-full`, `h-full` (fractions of the container) |
| `items-start/end/center/stretch` | `min-w-{n}`, `max-w-{n}`, `max-w-md` …, `min-h-{n}`, `max-h-{n}` |
| `grid-cols-{1–12}`, `grid-rows-{1–12}` | `self-start/end/center/stretch`, `col-span-{1–12}`, `col-span-full`, `row-span-{1–12}`, `row-span-full`, `m-*` |

Differences from CSS:
- An item's "natural" size is its `Size` in the Designer (the preferred size for AutoSize controls). Items without an explicit size do not shrink below their natural size.
- Item spacing only comes from `m-*` classes; the default 3 px WinForms `Margin` is not used.
- The default is `items-stretch`: items in a row stretch to the container's height, items in a column to its width.
- Grid items are placed in order with CSS auto-placement (sparse); cells covered by `row-span-*` are skipped. `grid-rows-N` splits the container height into N equal rows (`repeat(N, minmax(0, 1fr))`); `row-span-full` covers all of these explicit rows (a single row without `grid-rows`). Other rows take their height from the content; if an item spanning several rows does not fit, the missing height is shared equally by the rows it spans. There is no `col-start-*`, `row-start-*`, `grid-flow-*`, `margin: auto` or `order-*`.
- Items do not exceed the row's cross size (in CSS they may overflow; in WinForms the overflow would be clipped and lost). Shrunk labels show "…" with `truncate`.
- A `TwPanel`'s content starts inside its border (CSS box model); in an `AutoScroll` panel the width is taken from the visible area and room for the vertical scrollbar is reserved up front.
- A flex `TwPanel` with `AutoSize = true` sizes itself to its content (not yet for grid).
- Responsive variants (`sm:`, `md:` …): see [Animations and responsive variants](#animations-and-responsive-variants).

### Windows text size

Windows' **Text size** setting (Settings → Accessibility → Text size, 100–225%) enlarges the system fonts and with them WinForms' default font. `text-*` sizes are multiplied by it (like `rem` in a browser); before, a `text-sm` class shrank the user's enlarged text back to 14 px. Spacing and sizes are not multiplied; fixed-size buttons and labels trim their padding so the text fits, and fixed controls where the text does not fit even without padding are the application's layout (use `AutoSize` or `TwPanel` flex layout). If the setting changes while the application runs, styles are reapplied. Turn it off with `TwFonts.RespectTextScale = false` (for all providers). Tests simulate the setting (`TextScaleTests`, 150% and 225%).

## Font weights

`font-thin` (100), `font-extralight` (200), `font-light` (300), `font-normal` (400), `font-medium` (500), `font-semibold` (600), `font-bold` (700), `font-extrabold` (800), `font-black` (900).

WinForms only knows regular and bold; intermediate weights are installed on Windows as **separate font families**. FormWind resolves a weight to these families using CSS's matching rule:

| Class | Used with Segoe UI |
| --- | --- |
| `font-thin`, `font-extralight`, `font-light` | Segoe UI Light |
| `font-normal`, `font-medium` | Segoe UI |
| `font-semibold` | Segoe UI Semibold |
| `font-bold` | Segoe UI + bold style |
| `font-extrabold`, `font-black` | Segoe UI Black |

For other fonts, weight families are defined in the theme file; families that are not installed are skipped and the closest installed weight is chosen:

```jsonc
"fontWeights": { "Inter": { "300": "Inter Light", "600": "Inter SemiBold" } }
```

## Icons

The icon font shipped with Windows is used: **Segoe Fluent Icons** on Windows 11, **Segoe MDL2 Assets** on Windows 10 (no icons on Windows 7/8).

- **`TwIcon`**: a standalone icon. The `IconName` property is picked from a drop-down in the Designer. Color comes from `text-*`, size from the font size: `text-xl` = 20×20 px. AutoSize is on by default.
- **`TwButton.IconName`**: an icon next to the text. `IconPosition` (Left/Right) and `IconSpacing` (default 8 px) can be set.

```csharp
this.icoCustomer.IconName = "user";
this.tailwindProvider1.SetClasses(this.icoCustomer, "text-xl text-blue-600 dark:text-blue-400");
this.btnSave.IconName = "save";
```

Built-in names: `arrow-down`, `arrow-left`, `arrow-right`, `arrow-up`, `attach`, `bell`, `calculator`, `calendar`, `camera`, `cart`, `check`, `chevron-down`, `chevron-left`, `chevron-right`, `chevron-up`, `clock`, `copy`, `cut`, `download`, `edit`, `error`, `eye`, `eye-off`, `file`, `filter`, `flag`, `folder`, `folder-open`, `fullscreen`, `globe`, `grid`, `heart`, `heart-filled`, `help`, `history`, `home`, `image`, `info`, `key`, `link`, `list`, `location`, `lock`, `mail`, `map-pin`, `menu`, `minus`, `more`, `paste`, `phone`, `pin`, `plus`, `power`, `print`, `redo`, `refresh`, `save`, `search`, `send`, `settings`, `share`, `shield`, `sort`, `star`, `star-filled`, `sync`, `tag`, `trash`, `undo`, `unlock`, `upload`, `user`, `users`, `warning`, `x`, `zoom-in`, `zoom-out`.

Any glyph of the font can also be used by code point (`IconName = "#E8A5"`). Extra names and a different icon font come from the theme file:

```jsonc
"iconFont": "Segoe Fluent Icons",
"icons": { "invoice": "E8A5" }
```

Glyphs are centered on their actual ink bounds rather than the font metrics (the line height of icon fonts does not match the glyph size).

### SVG icons

Single-color SVG sets such as Lucide, Heroicons or Tabler can be used too. An `icons` value can be a path to an `.svg` file (relative to the theme file) or inline `<svg>` text; in code, `TwTheme.SetSvgIcon(name, svg)`:

```jsonc
"icons": {
  "invoice": "E8A5",                         // icon font glyph
  "package": "Assets/icons/package.svg",     // file
  "dot": "<svg viewBox='0 0 24 24'><circle cx='12' cy='12' r='4'/></svg>"
}
```

An SVG icon overrides the glyph with the same name, so built-in icons (`check`, `chevron-down` …) can be replaced one by one across all controls (`TwButton`, `TwSelect`, calendar, grid). The color is always the text color where it is drawn (like `currentColor`). Supported: `path` (all commands, including arcs), `circle`, `ellipse`, `rect` (`rx`), `line`, `polyline`, `polygon`, `g`; `fill`, `stroke`, `stroke-width`, `stroke-linecap`, `stroke-linejoin`. Gradients, masks, text and CSS are not supported.

## Borders, shadows and focus rings

| Class | Effect |
| --- | --- |
| `border-t`, `border-b-2`, `border-x`, `border-y` | Per-side borders (drawn without corners) |
| `shadow-sm`, `shadow`, `shadow-md`, `shadow-lg`, `shadow-xl`, `shadow-2xl`, `shadow-none` | Tailwind v3 shadow layers (blurred) |
| `shadow-{color}`, `shadow-blue-500/50` | Colored shadow |
| `ring`, `ring-0/1/2/4/8`, `ring-{color}`, `ring-offset-2`, `ring-offset-{color}` | Ring; usually `focus:ring-2 focus:ring-blue-500/30` |
| `bg-black/50`, `text-white/75` … | Opacity: `/0–100` on every color class |
| `backdrop-blur-sm`, `backdrop-blur`, `backdrop-blur-md` … `backdrop-blur-3xl`, `backdrop-blur-none` | Frosted glass on a `TwPanel`: what the parent paints behind it is blurred (4–64 px), under a translucent `bg-*/NN` |

Shadows and rings are drawn on the control's **parent** (controls cannot paint outside their own bounds); this works on Form, Panel and TwPanel parents. That is why native controls such as `TextBox` can have a focus ring too.

## Gradients

`bg-gradient-to-{r,l,t,b,tr,tl,br,bl} from-{color} via-{color} to-{color}` — drawn on `TwPanel`, `TwButton` and the standard `Panel`. A missing end is the transparent version of the other end.

## Interaction

- **`transition`** (`transition-all`, `duration-75` … `duration-1000`, `duration-[250ms]`): background, text and border color changes are animated (ease-out, 150 ms by default). Shadow and ring changes (`shadow-sm hover:shadow-lg transition`, `ring-0 focus:ring-2 transition`) are animated over the same duration too, the old one fading out as the new one fades in; blurred shadows come from the cache and are not recomputed during the transition.
  - What is animated: `transition-colors` only the colors (background, text, border), `transition-shadow` only the shadow and ring, `transition-none` nothing.
  - Curve and delay: `ease-out` (default), `ease-linear`, `ease-in`, `ease-in-out`; `delay-75` … `delay-1000`, `delay-[40ms]` holds the transition back that long. Example: `bg-white hover:bg-gray-100 transition duration-200 delay-75 ease-in-out`.
  - The first application has no transition: when classes are assigned, the control takes its final color at once (a transition would start from the application's own color and shift colors at startup). Theme, state (hover, focus, disabled …) and class changes are animated.
  - Transitions are advanced by a WinForms timer (about 60 frames/s). Windows only generates `WM_TIMER` when no other message is queued; while a window that invalidates itself in every `OnPaint` is open (e.g. ShareX's region capture) or the UI thread is busy, transitions stop at an intermediate color and finish once that window closes. The contrast audit checks the target color meanwhile.
- **`hover:` / `active:`**: only controls with these variants are restyled when the mouse enters or leaves; for the others, mouse movement does not trigger styling. If classes are assigned while the cursor is already over the control (e.g. a button whose classes change on click), the hover state is applied immediately.
- **`group` / `group-hover:`**: hovering a container with the `group` class applies the `group-hover:` variants of the controls inside it. A container that reacts to hover counts as hovered even when the cursor enters directly over a child.
- **Placeholder**: `TwTextBox.PlaceholderText`; color `placeholder:text-gray-400`.
- **`focus-visible:`**: applied only when focus came from the keyboard (Tab, arrows), not when the control was clicked (`focus-visible:ring-2 focus-visible:ring-blue-500/40`). Owner-drawn controls (`TwButton`, `TwTabStrip`, `TwDataList`, `TwCalendar`, `TwSlider`) draw their focus rings with the same rule. The last input type is available as `TwFocusVisible.LastInputWasKeyboard`.

## CheckBox, Radio, Switch

`TwCheckBox`, `TwRadioButton` and `TwSwitch` are owner-drawn. The checked color is `accent-*`, the unchecked fill `fill-*`, the box border `border-*`, the corners `rounded-*`, the text `text-*`. The `TwSwitch` (44×24 px) knob slides in 150 ms.

A disabled standard `Label` (without an image, not a `LinkLabel`) is redrawn the same way when its text color comes from the classes: Windows draws it in an embossed gray that is invisible in dark mode. The caption of a disabled `GroupBox` (with visual styles) is also drawn in a dark shade derived from the background and is redrawn the same way, in `GroupBoxRenderer`'s layout (seen in Bulk Crap Uninstaller while it loads).

While disabled, the standard `CheckBox` and `RadioButton` ignore `ForeColor` and draw the text in a dark shade derived from the background, which is invisible in dark mode. When the classes give the text color, FormWind redraws the disabled text beside the glyph: the `disabled:text-*` color if there is one, otherwise the text color mixed halfway toward the background. This is done for controls with the glyph on the left or right, `Appearance.Normal`, no image and a `FlatStyle` other than `System`.

## Menus and toolbars

A themed renderer for MenuStrip, ToolStrip, StatusStrip and ContextMenuStrip. Base classes apply to the strip, `menu:` to drop-down menus and `item:` to items:

```csharp
SetClasses(menuStrip1, "bg-white text-gray-700 border-b menu:bg-white menu:border-gray-200 item:hover:bg-gray-100 item:rounded-md divide-gray-200 dark:bg-gray-900 dark:text-gray-200");
```

- When `item:hover:`, the separator (`divide-*`) and the menu border (`menu:border-*`) are not given, the defaults are meant for a light strip. If the strip's background is dark, these colors are derived from the strip's own background and text colors, so the text does not disappear in the hover box on a dark strip.
- Drop-downs assigned to items by hand (`menuItem.DropDown = contextMenuStrip1`) do not inherit their owner's renderer in WinForms. FormWind gives these menus the strip's renderer (nested menus included). Menus with their own renderer or their own classes are left alone. A menu assigned to the strip later gets its colors the next time the strip is styled (e.g. on a theme change).
- The renderer is not reassigned unless the colors change (assigning it relayouts the strip). When classes are removed, the strip's original renderer is restored.

## Title bar

When a Form has classes and dark mode is on, the title bar turns dark automatically (Windows 10 20H1+). On Windows 11 it can be colored with `caption:bg-*`, `caption:text-*`, `caption:border-*`.

`backdrop-mica`, `backdrop-acrylic`, `backdrop-tabbed` (Mica Alt) and `backdrop-none` select the Windows 11 22H2+ system backdrop. The backdrop is visible in the title bar and window frame; it does not show inside the form because WinForms paints the client area opaque. If `caption:bg-*` is given, the caption color takes precedence over the backdrop.

## Dark mode: scrollbars and tooltips

With `DarkMode = true`, native parts that FormWind does not paint also switch to Windows' dark theme:

| Property (on by default) | Effect |
| --- | --- |
| `TailwindProvider.DarkScrollbars` | Scrollbars of Panel, TextBox, ListBox, TreeView, ListView and DataGridView; for a ListView also its column and group headers (Explorer's dark list theme; the header text is drawn in the list's text color. For `OwnerDraw` lists and ObjectListView the application handles the notification first; header cells it draws itself are left alone, those it leaves to Windows take the list's text color) |
| `TailwindProvider.DarkToolTips` | Native tooltip windows: `ToolTip` components, ToolStrip item tooltips, ListView/TreeView tooltips |

- **Disabled native controls:** Windows draws a disabled `ListView` and the edit area of an editable `ComboBox` in system colors, ignoring `BackColor`; they stayed light in dark mode (KeePass's entry list while no database is open, Bulk Crap Uninstaller while it loads). When the classes give a background, FormWind paints them in the class colors while disabled: the ListView's background (`WM_ERASEBKGND`, and the first custom draw stage of double-buffered lists) and items (`NM_CUSTOMDRAW`; a selected item gets a shade of the background instead of the system highlight), and the editable ComboBox's edit area (`WM_CTLCOLORSTATIC`). The text is the text color mixed halfway toward the background. `OwnerDraw` lists are left to the application. The themed box of a `DropDownList` ComboBox ignores `BackColor` too; with a dark background FormWind paints the box (enabled and disabled). `TextBox`, `ListBox`, `TreeView` and `NumericUpDown` follow the background on their own. Not covered: a disabled `RichTextBox` (see above).
- **The application's own theme:** some applications give lists and trees `SetWindowTheme(..., "explorer")` (KeePass: main window and options). That overrode FormWind's dark theme; group headers and the selected node stayed dark on a dark background. A `ListView` or `TreeView` in dark mode takes the dark theme back after the application's theme (`WM_THEMECHANGED`); in light mode the application's theme stays.
- **Text loaded as RTF:** text loaded into a `RichTextBox` with `Rtf`, `SelectedRtf` or `LoadFile` without a color table gets the "automatic" color, which Windows draws in the system text color (black); it was unreadable in dark mode (KeePass's entry details). When the classes give a text color, the automatic-colored runs get `ForeColor`; colors in the RTF, the selection, the scroll position and the undo history do not change.
- **Colors reset to the default:** if application code overrides the classes' color and then resets it with `ResetBackColor()` or `ResetForeColor()`, the color comes from the classes again right away (KeePass's repeat-password box: red on a mismatch, reset when it matches). A color that was set explicitly when the classes were assigned stays with the application.
- **DateTimePicker:** Windows draws the edit area, the up-down buttons and the drop-down calendar of the standard `DateTimePicker` from the theme and ignores `BackColor`; no theme name (`DarkMode_Explorer`, `DarkMode_CFD` …) darkens them. When the classes give a dark background (e.g. `dark:bg-gray-900 dark:text-gray-100`), FormWind renders Windows' drawing into a bitmap and inverts its lightness: neutral pixels map onto the control's colors (the theme's white onto `BackColor`, its black text onto `ForeColor`), colored ones (the selected field, the check box) keep their hue. The layout, the selected field and keyboard editing stay Windows'; meanwhile the text is drawn with grayscale antialiasing (ClearType's colored edges would stay colored after the inversion). The drop-down calendar is shown without the theme and takes the calendar colors from the classes (`menu:`, `selected:`/`accent-*`; see `TwDatePicker`). On a light background Windows' drawing is left alone. With visual styles off, the 3D border outside the client area is inverted the same way. `AutoStyle` gives a DateTimePicker the `select` component. For a date picker styled entirely with classes, use `TwDatePicker`.
- `DarkToolTips` follows the provider of the form that owns the tooltip: a tooltip is dark when its form (for a drop-down menu, the form of the strip it opens from) is styled by a provider in dark mode; tooltips of other forms on the same thread are left alone (for example the host application's, when FormWind is used by a plugin). Tooltips whose owner is not a WinForms control are dark while any provider on the thread is in dark mode. Open tooltip windows update when the theme changes.
- **With visual styles off** (the application does not call `Application.EnableVisualStyles`, `VisualStyleState` is off, or visual effects are turned off on Remote Desktop or in a locked-down environment): Windows' themes cannot apply, so `DarkMode_Explorer`/`DarkMode_ItemsView` (dark scrollbars, list theme, tooltips) silently do nothing. A dark `ListView`'s header stays classic light gray and FormWind leaves its text color alone (before, light text on light gray was unreadable, 1.04:1). The tab strip, DropDownList box, `ProgressBar` colors, PropertyGrid glyphs, `DateTimePicker`, flat buttons and redrawn disabled text do not depend on themes and work without them; the test suite runs in a process with visual styles off. With font smoothing off (Remote Desktop) the harness's screen contrast measurement sees the exact text color and raises no false alarms.
- Both use the undocumented `DarkMode_Explorer` theme used by Windows Explorer (Windows 10 1809+); it has no effect on unsupported systems and can be turned off if it causes problems.
- For a tooltip styled with classes, use `TwToolTip`.

## DataGridView

Parts of a DataGridView are styled with **part prefixes**, which can be combined with state prefixes (`dark:header:bg-gray-900`, `row:hover:bg-gray-50`). The built-in `table` component gives a light and dark table with a single class:

```csharp
this.tailwindProvider1.SetClasses(this.gridOrders, "table");
```

| Light | Dark |
| --- | --- |
| ![Grid, light](docs/grid-light.png) | ![Grid, dark](docs/grid-dark.png) |

The showcase sample's grid form (`FormWind.Showcase --grid`): search, filtering, grouping, totals, row details, the new cell types and saving the layout.

| Prefix | Target | Supported classes |
| --- | --- | --- |
| (none) | Control area and all cells | `bg-*`, `text-*`, font, `border`, `divide-x`, `divide-y`, `divide-{color}` (grid lines) |
| `cell:` | `DefaultCellStyle` | color, font, `px-*`/`py-*`, `text-left/center/right`, `whitespace-nowrap` |
| `alt:` | `AlternatingRowsDefaultCellStyle` | color, font |
| `selected:` | Selection colors | `bg-*`, `text-*` |
| `header:` | Column headers | color, font, padding, alignment, `h-*`, `border-0`, `whitespace-nowrap` |
| `row:` | Rows | `h-*` (row height), `row:hover:bg-*` / `row:hover:text-*` (row hover) |
| `row-header:` | Row headers | `hidden`, color |
| `match:` | Search matches ([Search](#search)) | `bg-*`, `text-*` |
| `group:` | Group headers ([Grouping](#grouping-and-totals)) | `bg-*`, `text-*`, font |
| `footer:` | Totals row (`TwGridFooter`) | `bg-*`, `text-*`, font |

**Variants of `table`** are written after it: `table-sm` (compact, 32 px rows), `table-lg` (48 px rows), `table-striped` (alternating rows), `table-bordered` (vertical and horizontal lines) — `"table table-sm table-striped"`. The Fluent, Ant Design and Material presets define their own `table-sm` and `table-lg` heights.

- When header colors are given, `EnableHeadersVisualStyles` is turned off (the Windows theme ignores header colors).
- When classes are removed, the grid's original styles are restored.
- In dark mode the scrollbars switch to Windows' dark scrollbars (`TailwindProvider.DarkScrollbars`, on by default).
- Part prefixes are ignored on controls other than DataGridView.
- On grids using `VirtualMode` or `DataSource`, `row:h-*` is supplied through the `RowHeightInfoNeeded` event: rows are not unshared, and the scroll position and current cell are preserved (~12 ms for 17,000 rows; ~17 s when setting rows one by one). On unbound grids only rows whose height differs are updated.
- The grid background (`BackgroundColor`) does not support transparency; a transparent `bg-*` falls back to the original color (it still applies to cells).
- **The application's cell colors:** the Designer's zebra background (`AlternatingRowsDefaultCellStyle.BackColor`) was chosen for the original cell background; when the classes set the cell background and `alt:` sets none, the zebra is kept but derived from the classes' background (the background moved 6% toward the text). Before, a light gray row kept light text in dark mode (AdvancedDataGridView sample). Columns with a background and no text color (`Column.DefaultCellStyle.BackColor`, e.g. a beige total column) are drawn with the original text color they were designed for even when the classes set the text color; text colors the application gives a cell or row are left alone. The contrast audit measures these pairs as `alternating row` and `column '…'`.
- **Row errors:** `row-header:hidden` (and `table`) hide the row headers; WinForms shows the icon of a row error (`DataGridViewRow.ErrorText`) only there, so errors became invisible. While the classes hide the row headers, a row with an error gets a 3 px bar in the danger color on its leading edge (on the right in RTL) and the error as its cells' tooltip (`CellToolTipTextNeeded`; on bound or `VirtualMode` grids). Cell errors (`Cell.ErrorText`) keep WinForms' own icon.
- When rows are added, the grid is repainted only if the empty-table or loading view changes.
- **Self-drawing grids:** grids that paint their own cells (e.g. the Git Extensions commit list) ignore cell styles and may depend on structural properties. `divide-x` / `divide-y` change `CellBorderStyle` and `row:h-*` changes the row height; do not use these classes if such a grid assumes `CellBorderStyle.None` or its own row height. The Git Extensions commit list hangs while scrolling with a `SingleHorizontal` border; the same happens without FormWind.
- **Dark mode for self-drawing grids:** `TwGrid.EnableDarkInversion(grid)` styles such a grid with its light classes in dark mode and inverts its picture with the hue kept (like the browsers' forced dark mode), so hard-coded light colors turn dark too. Images and colored marks are inverted as well; scrollbars, the editing control and row details are separate windows and get dark colors instead.

### Column classes

Every column has a **ColumnClasses on tailwindProvider1** property in its Properties window (`SetColumnClasses` in code). Base classes apply to the column's cells and `header:` to its header; `w-32`, `min-w-20`, `flex-1` (Fill weight) and `hidden` affect the column itself. If no font size is given, the grid's font is kept.

```csharp
tw.SetColumnClasses(colTotal, "text-right font-semibold w-28 header:text-right");
tw.SetColumnClasses(colName, "flex-1 min-w-40");
```

### Cell types

| Column | Value | Settings |
| --- | --- | --- |
| `TwBadgeColumn` | Text; a colored badge per value | `Variants = "Alive: bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-100; Dead: bg-red-100 text-red-800; *: bg-gray-100 text-gray-700"` |
| `TwAvatarColumn` | `Image` (or the `Placeholder` color) | `AvatarSize`, `Radius` |
| `TwIconColumn` | Icon name (`check`, `#E710`) | Color is the cell's text color |
| `TwProgressColumn` | A number from 0 to `Maximum` | `ShowText`, `BarColor`, `TrackColor`, `DarkTrackColor` |
| `TwCheckColumn` | `bool` / `CheckState` | Rounded box, `AccentColor`, `BoxSize`; `ThreeState` shows the indeterminate state as a dash |
| `TwButtonColumn` | Text (or `Text` + `UseColumnTextForButtonValue`) | `Classes = "rounded-md bg-red-600 text-white hover:bg-red-700"`; clicks via `CellContentClick` |
| `TwSparklineColumn` | Numbers (`IEnumerable`, or text separated by `;`/spaces) | `Kind` (`Line`, `Area`, `Bar`), `LineColor`, `DarkLineColor`, `NegativeColor` |
| `TwDateColumn` | `DateTime` / `DateTimeOffset` | Column format (default `g`); `Relative = true` shows the past week as "5 min ago" (`TwTexts.MinutesAgo` …); editing shows the date |
| `TwNumberColumn` | Number | Right-aligned, format `N2` (`C2` for currency), `Culture = "tr-TR"`, `NegativeClasses` (default `text-red-600 dark:text-red-400`), `PositiveClasses` |
| `TwLinkColumn` | Text | Theme blue (`DarkLinkColor` in dark mode), underlined under the mouse; clicks via `CellContentClick` |
| `TwTagsColumn` | Texts (or one text split at `Separator`) | Several badges with `Variants`; the ones that do not fit are counted as `+N` |
| `TwActionsColumn` | — | A "⋯" button with a menu: `Actions = "Edit; -; Delete"`, `ActionClicked`, `MenuOpening` (disable items per row), `Icon` |
| `TwExpanderColumn` | — | Chevron that expands the row's details ([Row details](#row-details)); click or Space |

Cell types learn the dark mode from the grid's provider; `dark:` variants need no extra setup.

### Row rules

```csharp
tw.SetRowClasses(gridOrders, row =>
    row.Cells["colStatus"].Value as string == "Cancelled" ? "text-gray-400 dark:text-gray-500" : null);
```

The rule returns classes for each row (`null` = no change); it is applied during `CellFormatting` and updates automatically when data changes. `SetRowClasses(grid, null)` removes the rule.

### Sorting, empty and loading states

On grids with classes, the sort arrow in the header is drawn with the theme icon (`arrow-up` / `arrow-down`) in the `accent-*` color. A grid bound to a `List<T>` or a plain `BindingList<T>` does not sort when its header is clicked; with `TwSortableBindingList<T>`, columns with `SortMode = Automatic` sort (strings with the current culture, including Turkish letter order; equal values keep their order):

```csharp
gridOrders.DataSource = new TwSortableBindingList<Order>(orders);
TwGrid.SetEmptyText(gridOrders, "No records");   // icon + text in the middle when there are no rows (TwGrid.SetEmptyIcon)
TwGrid.SetLoading(gridOrders, true);             // skeleton rows when empty, otherwise a faded overlay + "Loading…"
```

The TextBox/ComboBox opened while editing a cell is shown borderless in the cell's colors; its scrollbars turn dark in dark mode too.

### Filtering, column chooser and paging

```csharp
TwGrid.EnableFilterMenu(gridOrders);     // filter button in the header: the column's values as checkboxes
TwGrid.EnableColumnChooser(gridOrders);  // right-click on the header: show/hide columns
```

- The filter menu appears on grids whose data source is an `ITwFilterableList` (`TwSortableBindingList<T>`) and on columns with a `DataPropertyName`. A filtered column's button is drawn in the `accent-*` color. In code: `list.SetColumnFilter("City", new[] { "İzmir" })`, `ClearFilters()`.
- Sorts are stacked (in a list sorted by name and then by date, rows with the same date stay sorted by name); filtering keeps the sort order, and added or removed rows are reflected in the full list.
- The last visible column cannot be hidden from the column menu.
- Columns with eight or more values get a search box at the top of the menu; "Select all" then applies to the values shown.
- Number, date and duration columns of an `ITwGridSource` (`TwSortableBindingList<T>`) get **Min** and **Max** boxes, parsed with the current culture (`1.250,50`, `31.12.2025`); a date without time as the maximum covers that whole day. In code: `list.SetRangeFilter("Amount", 100, null)`. `ClearFilters()` removes the value and range filters.

### Search

```csharp
TwGrid.AttachSearchBox(gridOrders, txtSearch);   // searches as the user types (200 ms pause); Escape clears the box
TwGrid.SetSearchText(gridOrders, "izmir");        // in code; null ends the search
```

- Matching text is highlighted in plain text cells (`match:` colors; amber by default). The search ignores case with the current culture (`İZMİR` finds `izmir`).
- With a `TwSortableBindingList<T>` (`ITwGridSource`) only rows matching in a visible bound column stay; unbound grids hide the other rows; other bound grids only highlight.

### Grouping and totals

```csharp
TwGrid.SetGroupBy(gridOrders, colCity);           // a header per city with the row count; click it to collapse
TwGrid.SetGroupText(gridOrders, g => $"{g.Key} — {g.RowCount} orders");   // optional

var footer = TwGrid.AddFooter(gridOrders);        // totals row below the grid, in its container
footer.SetText(colCustomer, "Total");
footer.SetAggregate(colAmount, TwAggregate.Sum, "C2");   // Sum, Average, Count, Min, Max
```

- A `TwSortableBindingList<T>` orders the rows by the group first and keeps the current sorting within groups; rows added in code join the end of their group. With other data sources, consecutive rows with the same value form a group. `TwGrid.GetGroups`, `SetGroupExpanded`, `ExpandAllGroups`, `CollapseAllGroups`. Not supported in VirtualMode.
- The group header is added to the top of the group's first row (the row gets taller and its content moves down), so rows stay shared and scrolling stays smooth.
- `TwGridFooter` follows the grid's column widths, order and horizontal scrolling and recomputes when the rows or values change; with a filtered or searched list only the shown rows count. `footer.SetValue(column, values => ...)` computes a custom cell. `AddFooter` docks the footer below a grid docked to fill (or to the top/bottom); otherwise the grid gets shorter. It can also be placed in the Designer and connected with `Grid`.

### Tree rows

Hierarchical rows with columns (lines → machines → causes, each with its totals) in a grid, without a separate tree control:

```csharp
var tree = TwGrid.SetTree(grid, colName, lines, line => line.Children);   // or TwGridTreeNode("Press 3", 540, 6) nodes
tree.SetTotal(colMinutes, TwAggregate.Sum);                                 // parent rows show their children's sum
TwGrid.AddFooter(grid).SetAggregate(colMinutes, TwAggregate.Sum);          // counts only the rows without children
```

- The grid must be unbound (no `DataSource`): each node is a row, collapsed branches are hidden rows. Column values come from `TwGridTreeNode.Values` (in column order) or the `DataPropertyName` properties of `DataItem`.
- The tree column shows the indentation, an arrow and `IconName`. Click the arrow or double-click the cell (read-only grids) to expand and collapse; in the tree column → expands or moves to the first child and ← collapses or moves to the parent, `+`/`−` work in every column and `*` expands the whole branch. `NodeExpanded`, `NodeCollapsed`, `ExpandAll`, `CollapseAll`, `node.IsExpanded`.
- Header clicks sort the children of every parent; the hierarchy and the expanded branches stay (`tree.Sort(column, direction)`).
- `SetTotal` fills empty parent cells with `Sum`, `Average`, `Count`, `Min` or `Max` of their children (sums add the children's totals, the others look at the leaves). After changing nodes call `tree.Refresh()`.

### Row details

```csharp
gridOrders.Columns.Insert(0, new TwExpanderColumn());
TwGrid.SetRowDetails(gridOrders, row => new OrderLinesView((Order)row.DataBoundItem), height: 160);
```

- An expanded row shows the control below its cells; it moves with scrolling and is disposed when the row is collapsed. Expanded rows are remembered by their bound item, so sorting and filtering keep them. In code: `SetRowExpanded`, `ToggleRowDetails`, `IsRowExpanded`, `GetRowDetails`.

### Saving the layout and exporting

```csharp
Settings.Default.OrdersLayout = TwGrid.SaveLayout(gridOrders);   // JSON: column order, widths, visibility, sorting, grouping, search, filters
TwGrid.LoadLayout(gridOrders, Settings.Default.OrdersLayout);

TwGrid.ExportCsv(gridOrders, path);                 // UTF-8 with BOM (Excel); separator: the culture's list separator (; in Turkish)
TwGrid.CopyToClipboard(gridOrders);                 // selected cells (or all rows) as tab-separated text with headers
string text = TwGrid.ToDelimitedText(gridOrders, ';', selectedOnly: false);
```

- Columns are identified by `Name`; columns missing from the grid and invalid values are skipped, so a layout saved by an older version of the form still loads.
- The export writes the visible columns in display order and the visible rows, formatted with each column's format; fields with the separator, quotes or line breaks are quoted. Button, image, expander and actions columns are skipped.

**Large tables (VirtualMode):** `TwVirtualRows<T>` puts the grid in VirtualMode and loads rows page by page as they come into view; the most recently used pages are cached, so with a million rows only the visible pages are in memory:

```csharp
var rows = new TwVirtualRows<Order>(gridOrders, (start, count) => db.Orders.OrderBy(o => o.Id).Skip(start).Take(count).ToList(), pageSize: 100);
rows.Reset(db.Orders.Count());              // again when the data or the sort order changes
```

- A cell's value is read from the property named by the column's `DataPropertyName` (or its name); change it with `rows.CellValue = (order, column) => ...`. `rows.GetItem(rowIndex)` returns the row's data.
- `loadPage` runs on the UI thread and should be fast (OFFSET/FETCH in the database). In VirtualMode the grid cannot sort by itself: sort in the query and call `Reset`. Adding rows by the user is turned off.

`TwPager` draws paging buttons (‹ 1 … 9 10 11 … 20 ›); you bind the data for the page:

```csharp
pager.TotalItems = orders.Count;
pager.PageSize = 20;
pager.PageChanged += (s, e) => grid.DataSource = orders.Skip(pager.FirstItemIndex).Take(pager.PageSize).ToList();
tw.SetClasses(pager, "text-gray-700 selected:bg-blue-600 selected:text-white item:hover:bg-gray-100");
```

### Variant order

Later overrides earlier: base → `dark:` → `checked:` → `focus:` → `focus-visible:` → `group-hover:` → `hover:` → `dark:hover:` → `active:` → `disabled:` → `invalid:` → `sm:` → `md:` → `lg:` → `xl:` → `2xl:` → `forced-colors:` (width variants come last, as in Tailwind).
`focus`, `hover` and `active` are not applied while a control is disabled.

## Theme file (`tw.config.json`)

```jsonc
{
  "version": 1,                                          // file format version (optional)
  "preset": "fluent",                                     // ready-made look; the rest of the file is applied on top
  "fontFamily": "Segoe UI",
  "spacingUnit": 4,
  "colors": {
    "brand": { "500": "#2b579a", "600": "#234a85" },   // bg-brand-500
    "canvas": "#f3f6fb"                                  // bg-canvas
  },
  "components": {
    "btn-primary": "btn bg-brand-500 text-white hover:bg-brand-600"
  }
}
```

`fontFamily` can be a comma-separated stack (`"Inter, Segoe UI"`): the first installed or registered family is used. Fonts shipped with the application are registered without installation via `"fonts": ["Assets/fonts/Inter-Regular.ttf", "Assets/fonts/Inter-SemiBold.ttf"]` (paths are relative to the theme file; missing files are skipped); in code, `TwFonts.Register(path)`. Fonts are registered with both GDI+ (`PrivateFontCollection`) and GDI (`AddFontResourceEx`, process-private), so they also work in standard controls drawn with `TextRenderer`. Distribute the font license (e.g. SIL OFL) with your application.

**Tailwind v4 palette.** `"palette": "v4"` (in code `new TwTheme(TwPaletteVersion.V4)` or `theme.UsePalette(...)`) uses the v4 shades of the same 22 colors. v4 colors are defined in OKLCH (`TwPaletteV4`); those outside sRGB are approximated with CSS Color 4 gamut mapping, as browsers do, so `blue-500` becomes `#2b7fff` just like on the Tailwind site. Colors in the theme file can also be written as `oklch(62.3% 0.214 259.8)`; in code, `TwOklch.TryParse`. The default components pass the contrast audit with both palettes.

The file is added on top of the default palette and components (or the preset chosen with `preset`). For the list of components see [Presets](#presets-and-autostyle) and `TwTheme.DefaultComponents`; the `autoStyle` key changes the AutoStyle mapping. The Designer's preview and lists use the project's theme file too (see [Designer](#designer)). `version` is the file format version (see [API stability](#api-stability)).

## Controls

| Control | When to use |
| --- | --- |
| `TwPictureBox` | Image with smooth rounded corners (`rounded-lg`, `rounded-full` avatar); `object-cover` / `object-contain` / `object-fill`. |
| `TwSelect` | Stylable drop-down (instead of the native ComboBox): the box with base classes, the menu with `menu:` / `item:`. By default the width follows the selected item (`SizeToSelection`). Typing selects the item starting with those letters; `Searchable = true` adds a search box above the menu that filters items (ignoring case, accents and the ı/i difference). |
| `TwIcon` | Standalone icon (see [Icons](#icons)). |
| `TwCheckBox`, `TwRadioButton`, `TwSwitch` | See [CheckBox, Radio, Switch](#checkbox-radio-switch). |
| Standard controls | Color, text and spacing work on every control. Corners are clipped with `Region` (no anti-aliasing) and borders are drawn in the `Paint` event. On a `TextBox` (`TextBoxBase`) the native border is turned off; `border-*` and `rounded-*` are drawn on the parent as a frame 3 px outside the control, filled with the background color (the frame extends past the control's bounds; leave room in the layout). The closed box of a `ComboBox` in `DropDownList` mode (background, border, corners, text, arrow) is painted by FormWind with border or corner classes and on a dark background (without a border class, with a 1 px border between the background and the text color); the drop-down list stays native. Editable `ComboBox`es and other native controls (`ListBox` …) get no corners or borders; use `TwSelect`, `TwListBox`. |
| `TwButton` | Button with smooth corners. `Image` is not drawn yet and the `Paint` event is not raised. |
| `TwPanel` | Panel (card) with smooth corners and a border. A translucent background (`bg-white/70`) shows the parent's painting (its gradient or image) through; `backdrop-blur-*` blurs it first: a frosted card or sidebar inside the window, the in-window counterpart of `backdrop-acrylic`. |
| `TwTextBox` | Wraps a borderless TextBox in a rounded panel; for the `input` style. |
| `TwNumberBox` | Number box (`TwTextBox` + ▲▼ on the right). `Minimum`, `Maximum`, `Increment`, `DecimalPlaces`; arrow keys, mouse wheel and culture-aware parsing. |
| `TwDatePicker` | Date field + drop-down `TwCalendar`. The date can be typed (parsed with `Culture`/`Format`); F4 / Alt+↓ opens the calendar. The calendar is styled with `menu:bg-* menu:border-*`, `selected:bg-* selected:text-*`, `item:hover:bg-*`. |
| `TwPopover` | A panel that opens next to a control (component): shows the `Content` control on the `Placement` side (`BottomStart`, `TopEnd`, `Right` …) and flips to the opposite side when it does not fit on the screen. A click outside or Esc closes it; `Toggle(anchor)` closes it when open, `RestoreFocus` returns the focus to the anchor. `TwDatePicker`'s calendar opens in one. |
| `TwCalendar` | Standalone month calendar: the culture's first day of week and day names, a ring on today, `MinDate`/`MaxDate`, keyboard (arrows, PageUp/PageDown, Home/End, Enter, Esc). |
| `TwTabStrip` | Tab strip: `Underline`, `Pills`, `Segmented`. `selected:text-* selected:border-*` / `selected:bg-*`, `item:hover:*`, `fill-*` (segmented background), `gap-*`. Tabs that do not fit go to a "more" (…) button at the end (`OverflowMenu`, ↓ opens it; the menu takes `menu:` / `item:`); the selected tab always stays visible. `&` marks a tab's Alt shortcut as on a Label; `UseMnemonic = false` draws it as text ("Parts & cost"). Only the strip; for pages use `TwTabControl`. |
| `TwTabControl` | Tab control with pages (instead of TabControl): `AddPage("General")` or add `TwTabPage`s; the selected page is shown. The strip is styled through `Strip` (`provider.SetClasses(tabs.Strip, "...")`); Ctrl+Tab / Ctrl+PageDown move between pages. |
| `TwSlider` | Slider (instead of TrackBar): `Minimum`, `Maximum`, `Value`, `SmallChange`, `LargeChange`. Track `fill-*`, filled part `accent-*`, thumb `item:bg-*`. Keyboard, mouse wheel and dragging. |
| `TwProgress` | Progress bar (instead of ProgressBar): track `fill-*`, fill `accent-*`, corners `rounded-*`. `Indeterminate = true` draws a moving segment for work of unknown length. |
| `TwDataList` | Virtual list (see below). |
| `TwListBox` | Instead of ListBox: `Items` (+ `DisplayMember`), `SelectedItem(s)`, `FindString`; with TwDataList's virtual drawing, multi-selection and styles. `TwListItem` items get the built-in row template (see [List rows](#list-rows-twlistitem)). |
| `TwCheckedListBox` | Instead of CheckedListBox: a rounded checkbox at the start of each row (`accent-*`), `CheckedIndices`/`CheckedItems`, `ItemCheck`; click the box or press Space. |
| `TwTreeView` | Virtual tree instead of TreeView: `Nodes`, nodes with icons, `SelectedNode`, `ExpandAll`; ←/→ expand/collapse, `HasLazyChildren` + `BeforeExpand` for lazy loading. Exposed to screen readers as an Outline. |
| `TwPager` | Paging (see [DataGridView](#datagridview)). |
| `TwField` | Label + editor + hint/error text; validation rules (see [Forms](#forms-validation-masks-and-dialogs)). |
| `TwToolTip` | Stylable tooltip: `Classes`; follows dark mode when `Provider` is set. |
| `TwChip` | A pill with a text, an optional icon or status dot (`IconName`, `DotColor`) and an optional ✕ (`Removable` → `RemoveClick`, also Delete/Backspace). `Clickable` makes it a button, `Checkable` a filter chip that turns on and off (`Checked`, styled with `checked:`). Display chips are not tab stops. Components `chip`, `chip-primary`, `chip-success`, `chip-warning`, `chip-danger`; the ✕ is a separate "Remove" button for screen readers. |
| `TwToolbar` | A responsive toolbar (a `TwPanel`): children sit in one row with `gap-*`; items that do not fit move to a "more" (…) button whose menu runs them (a button is clicked, a check box toggled, a `TwTabStrip` or `TwSelect` becomes a submenu, other items raise `OverflowItemClick`). Per item: `SetAlignment` (`Start`/`End`), `SetPriority` (higher moves later), `SetOverflow` (`AsNeeded`, `Never`, `Always`, `Shrink`: a title gets narrower before anything moves, down to its `MinimumSize.Width`). The menu takes `menu:` / `item:`, the button is `OverflowButton`. |
| `TwSettingRow` | A settings row (System Settings style): `LabelText`, `Description` (muted, wraps), the editor (the child control) and `UnitText` on the right (`EditorWidth`, 160 by default). Rows stacked in a `card p-0` panel draw dividers between them (`divide-*`); narrow rows put the editor under the text. `label:` and `hint:` parts, `setting` component. |

### TwDataList

A virtual list that draws only the visible rows; the data is not stored in the list, rows are requested by index. Drawing time depends on the number of visible rows, even with a million rows.

```csharp
list.ItemCount = orders.Count;
list.ItemHeight = 52;                       // 96 DPI pixels
list.DrawItem += (s, e) =>
{
    var o = orders[e.Index];
    var c = e.ContentBounds;
    int left = e.DrawBadge(o.Status, c.Right, badgeBack, badgeFore);
    e.DrawText(o.Customer, new Rectangle(c.X, c.Y + 6, left - c.X - 12, 20), font: e.FontWithWeight(600));
    e.DrawText(o.Number, new Rectangle(c.X, c.Y + 26, left - c.X - 12, 20), e.MutedColor);
};
tw.SetClasses(list, "p-1 bg-white text-gray-900 item:hover:bg-gray-100 selected:bg-blue-50 selected:text-blue-900 "
                  + "row:border-gray-100 fill-gray-300 dark:bg-gray-800 dark:text-gray-100 dark:selected:bg-blue-950");
```

- Without `DrawItem`, `ItemText` (or the index) is drawn as a single line of text.
- The mouse wheel scrolls smoothly (`SmoothScrolling`); the scrollbar is owner-drawn, draggable and colored with `fill-*`.
- Keyboard: ↑/↓, PageUp/PageDown, Home/End; Enter and double-click raise `ItemActivate`.
- `item:rounded-*` row corners, `item:text-*` secondary text, `row:border-*` row separator, `ItemPadding` inner padding (`e.ContentBounds`).
- **Multi-selection:** `SelectionMode = MultiExtended`; Ctrl+click adds/removes, Shift+click and Shift+arrow select a range, Ctrl+A selects all (without storing indices, even with a million rows). `SelectedIndices`, `IsSelected`, `SetSelected`, `SelectionChanged`.
- **Groups:** `SetGroups(new[] { (0, "Today"), (12, "Yesterday") })`; the header of the group at the top sticks while scrolling. Headers are drawn with `header:bg-* header:text-*` or the `DrawGroupHeader` event.
- **Rows of different heights:** `ItemHeights = i => orders[i].HasNote ? 72 : 52` (96 DPI pixels); row offsets are computed once, call `InvalidateItemHeights()` when the heights change. Without it every row is `ItemHeight` tall and a million rows cost nothing extra; with it each row's height is asked for once.
- **Incremental loading:** `IsItemLoaded` draws unloaded rows as skeletons (with a gentle pulse) and `ItemsNeeded(first, last)` is raised with the visible range; call `Invalidate()` when the data arrives.

### List rows (`TwListItem`)

The "title, muted line, status dot, value and badge on the right" row that lists repeat (a sidebar, a queue, alerts, rules, search results) comes ready-made: add `TwListItem`s to a `TwListBox` and they are drawn with the built-in template; parts that are not set take no room.

```csharp
list.Items.Add(new TwListItem("Press 3 · spindle bearing", "Line 2 · 6 stops in 30 days")
{
    DotColor = Color.Red,                     // or IconName = "warning", or Score = 0.72 (a small ring with "72")
    Value = "540 min", ValueCaption = "30 days",
    Badge = "Chronic", BadgeClasses = "chip-danger",
});
```

- Rows with a second line (`Subtitle` or `ValueCaption`) get two lines' height on their own; single-line rows keep `ItemHeight`.
- The badge takes its colors, padding, corners and font from `BadgeClasses` (default `chip`, with `dark:` variants); the score ring's arc is `ScoreColor` or `accent-*`.
- Screen readers read the parts joined (`"Press 3 · spindle bearing, Line 2 · 6 stops in 30 days, 540 min, 30 days, Chronic"`). Handling `DrawItem` turns the template off.

## Charts

Lightweight charts drawn by FormWind itself, with no extra dependency: `TwChart` (line, area, bar), `TwDonutChart`, `TwSparkline` and the KPI card `TwStat`. They follow the dark mode, DPI and the theme like the other controls; with `AutoStyle` they get the `chart` and `stat` components.

| Light | Dark |
| --- | --- |
| ![Charts, light](docs/charts-light.png) | ![Charts, dark](docs/charts-dark.png) |

The dashboard ships in the sample: `FormWind.Showcase --charts` (clicking a month, quarter or slice updates the status line through `ItemClick`).

```csharp
chart.Kind = TwChartKind.Bar;                    // Line, Area, Bar; Stacked = true stacks the bars
chart.SetData(new[] { "Jan", "Feb", "Mar" },
    new TwChartSeries("Revenue", new[] { 120.0, 150, 170 }),
    new TwChartSeries("Target", new[] { 130.0, 140, 160 }) { Kind = TwChartKind.Line });   // a line over the bars
chart.ValueFormat = "C0";                        // tooltip; axis ticks are compact (1.2K) unless AxisFormat is set

donut.SetData(new[] { "Web", "Store", "Phone" }, new[] { 52.0, 28, 20 });

stat.Title = "Revenue"; stat.Value = "$1.24M"; stat.Delta = 0.124; stat.Caption = "vs last month";
stat.Trend = new[] { 820.0, 940, 910, 1040, 1240 };
stat.PositiveIsGood = false;                     // for costs and error rates: a rise is shown in red
stat.ShortValue = "278 h";                       // shown when "278 h 52 min" does not fit; then the text gets smaller

breakdown.Kind = TwChartKind.HorizontalBar;      // one row per category with its full label on the left
heatmap.EmptyCells = TwHeatmapEmptyCell.Hatched; // NaN cells: hatched gray, "No production" in the legend and tooltip
heatmap.EmptyCellText = "No production";
```

| Class | Effect |
| --- | --- |
| `bg-*`, `text-*`, font | Background, text color (values, legend), font |
| `label:text-*` | Axis labels, legend values, titles and captions (secondary text) |
| `divide-*` | Gridlines |
| `accent-*` | Color of the first series and of sparklines |
| `stat`: `border`, `rounded-*`, `p-*` | Card look of `TwStat` |

- **Colors** come from a fixed, validated categorical palette (distinguishable with color-vision deficiencies, separate light and dark steps); series take it in order and keep their color when others are added or removed. A ninth series is gray instead of a recycled hue; `TwDonutChart` folds slices past eight into `TwTexts.Other`. `TwChartSeries.Color` and `TwChartSlice.Color` set a color explicitly.
- **Marks:** bars at most 24 px wide with a rounded 4 px end and square at the baseline, 2 px gaps between bars, stacked segments and slices; 2 px lines; areas at 10% opacity; markers only on the last point and the highlighted category. A single value axis; bar and area charts include zero. `double.NaN` is a missing value (a gap in the line).
- **Legend:** shown for two or more series (`ShowLegend`); the key has the series' mark (a short line for lines, a square for bars). Text never takes the series color.
- **Interaction:** hovering or the arrow keys (Home/End) highlight a category and show a tooltip with every series' value; Enter or a click raises `ItemClick` (`TwChartItemEventArgs.Index`, `Label`), `HoverIndexChanged` follows the highlight. The donut highlights a slice and shows it in the middle.
- **Screen readers:** the charts have the `Chart` role with one item per category or slice (`"Mar: Revenue 170, Target 160"`, `"Web: 52 (52%)"`); the value of the chart is a summary of the ranges. `TwStat` reads as "title: value, change, caption"; the change always carries an arrow and a sign, never color alone.
- `TwStat`'s change uses the theme's `success` and `danger` colors, so presets and theme files recolor it.
- **Horizontal bars** (`TwChartKind.HorizontalBar`, set on the chart): categories are rows with their full label on the left (up to 40% of the width, then "…"), the value axis runs along the bottom and values (`ShowValues`) sit at the bar ends, with no axis when every series shows them. Series are side by side or `Stacked`; reference lines are vertical; ↑/↓ move between rows.
- **Fitting `TwStat`:** a value wider than the card switches to `ShortValue`, then gets smaller down to the title's size (`ValueFit = Shrink`, the default) instead of being cut with "…" (`Ellipsis`); screen readers always read the full value.
- **Empty heatmap cells:** `EmptyCells` draws `double.NaN` cells in the lowest shade (`Low`, as before), flat gray (`Solid`), hatched gray (`Hatched`) or not at all (`None`); `EmptyCellColor` sets the gray, and `EmptyCellText` ("No production") replaces "—" in the tooltip and for screen readers and adds a key to the legend.
- **Small rings:** a `TwRingChart` the size of an icon (32 px) keeps a ring at least 3 px thick and shrinks its center text to fit; `TwListItem.Score` draws the same score ring inside a list row.

## Forms: validation, masks and dialogs

```csharp
var field = new TwField { LabelText = "National ID", Required = true, Validator = TwValidators.TcKimlikNo };
field.Controls.Add(new TwTextBox { Mask = TwMasks.TcKimlikNo });
tw.SetClasses(field, "field");                 // label:, hint:, error: parts
// On Save:
if (!tw.Validate(this)) return;                // shows the errors and focuses the first invalid field
```

- **`TwField`**: a label (" *" when required), a single editor and a hint or error text below it. Rules: `Required`, `Pattern` (regular expression), `Validator` (custom), completeness for masked fields. The label becomes the editor's `AccessibleName` and the error text its `AccessibleDescription`.
- **`tw.SetError(control, "message")`**: a manual error (e.g. from the server). While there is an error, the `invalid:` variant is applied; the `input` component includes a red border and ring. The error clears itself when the user changes the value. `HasErrors`, `ClearErrors()`, `ErrorsChanged`.
- **Built-in rules** (`TwValidators`): `TcKimlikNo` (Turkish national ID check digits), `Iban` (mod 97), `Email`, `TurkishMobile`, `MinLength(n)`.
- **Masks** (`TwTextBox.Mask`): `0` digit, `9` optional digit, `L` letter; built-in masks `TwMasks.Phone`, `Iban`, `TcKimlikNo`, `Date`, `PostalCode`. Only matching characters can be typed; pasting skips literal characters. `UnmaskedText`, `MaskCompleted`.

```csharp
if (TwDialog.Show(this, "Delete this order?", "Confirm delete", TwDialogKind.Warning, TwDialogButtons.YesNo, danger: true) == DialogResult.Yes) …
TwToast.Show(this, "Order saved", TwDialogKind.Success);
```

- **`TwDialog`** (instead of MessageBox): icon, title, message and right-aligned buttons; Enter is the default, Esc cancels/answers no. Follows the owner form's dark mode. Button texts come from [`TwTexts`](#localization) and can be overridden one by one with `TwDialog.Texts[DialogResult.Yes] = "Delete"`; `TwDialog.Create(...)` allows customization before showing.
- **Text input:** `TwDialog.Prompt(this, "Name of the saved search", "Save search", placeholder: "e.g. Line 2 · night shift", validator: name => names.Contains(name) ? "This name is taken." : null)` returns the trimmed text, or `null` when cancelled. With `TwDialogOptions.ShowInput` (`InputText`, `InputLabel`, `InputPlaceholder`, `InputRequired`, `InputMaxLength`, `InputValidator`) any dialog gets a text box; OK checks it and keeps the dialog open with the error under the box.
- **`TwToast`**: a notification that stacks in a corner of the owner form (bottom-right by default, `TwToast.Position`), does not steal focus, pauses while hovered and closes by itself (`DefaultDuration` 4 s). Announced to screen readers as an Alert.

## Localization

User-facing texts (dialog buttons, validation messages, the grid filter menu and loading text, the search box hint, screen reader names) live in `TwTexts`. The default language is English; Turkish texts are selected when the application's UI language (`CultureInfo.CurrentUICulture`) is Turkish.

```csharp
TwTexts.Current = TwTexts.Turkish;                                   // force a language
TwTexts.Current = new TwTexts { Ok = "OK", Cancel = "Abbrechen", Required = "Pflichtfeld." };   // another language
TwTexts.Current.Loading = "Fetching data…";                          // a single text
```

- A control's own text properties (`TwField.RequiredMessage`, `PatternMessage`, `IncompleteMessage`, `TwSelect.SearchPlaceholder`) take precedence over `TwTexts` when set; when not set, they are not written to Designer.cs.
- Formatted texts use `{0}` placeholders (`MinLength`: number of characters, `Page`: page and page count).
- In grid filtering, the key for blank values is language-independent (`TwSortableBindingList<T>.EmptyValue`); the menu shows `TwTexts.Blank`.
- Developer-facing texts (exceptions, Designer commands, analyzer warnings, Debug output) are in English.

## Accessibility

Owner-drawn controls are exposed with their items to screen readers and UI automation tools (Narrator, NVDA, FlaUI, Appium/WinAppDriver):

| Control | Role | Items |
| --- | --- | --- |
| `TwDataList` | List | Each row is a ListItem; name from `AccessibleItemText` (else `ItemText`, else "Item N"). Virtual: item objects are created on demand. |
| `TwTabStrip` | PageTabList | Each tab is a PageTab (with its selected state and default action) |
| `TwCalendar` | Table | "Previous month" / "Next month" buttons and days (name: "15 October 2026 Thursday"; days out of range are Unavailable) |
| `TwSelect` | ComboBox | The value is the selected item; the default action opens the menu |
| `TwSlider` / `TwProgress` | Slider / ProgressBar | Value |
| `TwTextBox`, `TwNumberBox`, `TwDatePicker` | (inner TextBox) | Name and description from the wrapper's `AccessibleName` / `AccessibleDescription`, else from the placeholder text |

Selection and focus changes are announced to screen readers. A control's `AccessibleRole` is used when set. Native parts FormWind draws in place of Windows (the tab strip, disabled text, the dark `DropDownList` box, disabled lists) change only the image; name, role, state and value stay as Windows reports them. Because these parts also cover Windows' focus rectangle, FormWind draws the focus rectangle on a focused tab strip and box while keyboard cues are on (Windows hides them until the keyboard is used). The hidden label that draws disabled text does not keep the application's text. Give fields without a visible label an `AccessibleName` (see `ControlsForm`). `TwTreeView` appears as Outline/OutlineItem (with expanded state and level), `TwPager` as ToolBar ("Page 3 of 20"), `TwToast` as Alert and `TwDialog` as Dialog.

UI Automation tests (`Category=UIAutomation`) verify the list, tabs, drop-down and calendar, and the native controls FormWind paints (`NativeUiAutomationTests`), from outside, using the client that screen readers use. Because this client changes process-wide settings (text is drawn differently in later tests), these tests are skipped in the default run; run them separately:

```powershell
$env:FORMWIND_UIA = "1"; dotnet test tests/FormWind.Tests -f net472 --filter Category=UIAutomation
```

### High Contrast mode

When High Contrast is on in Windows (Settings → Accessibility → Contrast themes), FormWind uses system colors instead of class colors:

- Control backgrounds and text return to their original (system) values; selected and hovered parts (`selected:`, `item:hover:`, `row:hover:`) get the highlight color (`Highlight`).
- Borders and separators from classes are drawn in the text color; shadows, gradients and color transitions are not drawn. `TwButton`s without a border get a 1 px border. Standard buttons with a background class (FormWind makes them flat) also get a border in the system text color; before, once the background returned to the system color they looked like plain text.
- Spacing, font size, corners and layout do not change. If the mode changes while the application runs, styles are reapplied.
- Colors specific to this mode are given with the `forced-colors:` variant (as in Tailwind): `forced-colors:border-2`.
- To turn this off, set `TailwindProvider.RespectHighContrast = false`. Whether the mode is active can be read from `HighContrastActive`; code that draws by itself (e.g. badge colors in `DrawItem`) should check it.
- Grid cell types (`TwBadgeColumn`, `TwProgressColumn` …) keep their own colors.
- What FormWind draws in place of Windows returns to Windows in this mode: the standard `TabControl`'s tab strip, `ProgressBar` colors, dark theme names (scrollbars, the ListView theme and header text, the `PropertyGrid` scrollbar, `DarkToolTips`), the title bar's dark mode, `caption:` colors and backdrop, `LinkLabel`'s `accent-*` link colors (the highlight color does not become the link color) and the PropertyGrid's +/− glyphs. Where disabled text is redrawn (flat buttons) it is `GrayText`, and the row error bar of `row-header:hidden` grids is `Highlight`; `disabled:text-*` class colors are not used. Disabled ListView and editable ComboBox painting, the DateTimePicker and the dark `DropDownList` box are already off, as there is no class background. `HighContrastTests` checks these by turning the `TwHighContrast` simulation on and off (without touching the system setting).

### Contrast audit

`TwContrast` checks a form's current colors against WCAG 2 contrast ratios. Use it in tests or during development to verify the light and dark versions of a theme:

```csharp
tw.DarkMode = true;
foreach (var issue in TwContrast.Audit(this, tw))
    Debug.WriteLine(issue);   // TwButton 'btnSave' hover: 2.54:1 < 4.5:1 (#FFFFFF on #60A5FA)
```

| Checked | Section name |
| --- | --- |
| Controls with text (Label, button, TextBox, ComboBox, list, tree, GroupBox …) | `text` |
| The `hover:` variant in the classes (when a provider is given; no need to move the mouse) | `hover` |
| ToolStrips and menus: strip, item hover, drop-down menu, menu item hover | `strip`, `item:hover`, `menu`, `menu item:hover` |
| DataGridView cells, selected cells, headers, alternating rows and columns with a background | `cell`, `selected`, `header`, `alternating row`, `column '…'` |

- The threshold is 4.5:1 for normal text and 3:1 for large text (18 pt, or 14 pt bold) (WCAG AA). It can be changed with the `minimum` parameter.
- Transparent backgrounds are blended with the parent's background. Hidden controls are skipped.
- Disabled controls are outside WCAG's scope and are skipped by default. `includeDisabled: true` checks them too (part name `disabled text`); this catches disabled controls used to display content (e.g. a description box disabled so that no caret shows). For disabled native text boxes the text is the system gray and the background is the assigned background on a `TextBox` and the system background Windows paints on a `RichTextBox`; the threshold is 3:1, as for other disabled controls. The harness runs the audit with this option. For disabled buttons and for check boxes whose text FormWind redraws, the text color actually drawn is checked against 3:1: the aim is to catch invisible text, not to enforce contrast.
- For colors in the middle of a `transition`, the target color is checked; the audit can be called right after a theme change.
- `issue.Cause` gives a known cause: `KeptBackground` when the classes give only the text color and the background stays a color the application set explicitly (different from the parent's); the fix is a `bg-*` class or letting [copied backgrounds](#presets-and-autostyle) follow the parent. `ToString()` mentions the cause too.
- `TwContrast.Ratio(foreground, background)` returns the ratio between two colors.
- `TwContrast.FindOwnerDrawn(root)` lists controls that paint their own colors (grids with `CellPainting` handlers or an overridden `OnPaint` — FormWind's own grid handlers do not count, lists, trees, tabs and combo boxes with `OwnerDraw`/`DrawMode` on). The audit cannot know their colors on screen; check them by eye in dark mode. Painting code can get the colors with `tw.GetColors(…)`.
- The default components (`btn-primary`, `btn-ghost`, `card`, `input`, `table`) pass this audit in light and dark mode (`ContrastTests`). `btn-primary` uses `blue-600`/`blue-700` in dark mode too; white text stays below the threshold on `blue-500` (3.7:1) and `blue-400` (2.5:1).

## Error handling

An error while applying styles does not crash the application: the exception is caught, the control keeps the style applied so far, and the error is reported through the `TailwindProvider.StyleError` event (or the Debug output when nobody listens). Exceptions thrown by a custom `IStyleApplier` or a `SetRowClasses` rule are reported this way; errors while painting shadows, borders and ComboBoxes do not stop painting either.

```csharp
tw.StyleError += (s, e) => logger.LogWarning(e.Exception, "Could not style {Target}: {Classes}", e.Target, e.Classes);
```

- A listener may rethrow the exception (e.g. to keep errors visible in tests).
- Critical exceptions such as out-of-memory are not caught.
- The failing control is retried on the next state change (hover, theme …).

## Right-to-left (RTL)

When a control's inherited `RightToLeft` is `Yes`:

- The `rtl:` and `ltr:` variants apply by direction (as in Tailwind): `pl-4 rtl:pl-0 rtl:pr-4`. Styles are reapplied when the direction changes.
- `TwPanel` flex and grid layouts are mirrored horizontally (like CSS `dir="rtl"`): the first item is on the right and `justify-start` aligns to the right. Margins swap with the mirror (`ml-*` acts as the start side).
- `TwButton` (the icon moves to the other side of the text, text alignment is mirrored), `TwCheckBox`, `TwRadioButton`, `TwSwitch` (indicator on the right, "on" position on the left) and `TwSelect` (arrow on the left) are mirrored.
- If the form has `RightToLeftLayout = true`, Windows mirrors the windows itself and FormWind does not mirror again.
- What FormWind draws in place of Windows follows Windows' RTL layout: disabled label text is right-aligned and the glyph of check boxes and radio buttons moves to the other side; on the standard `TabControl`'s tab strip the tab order and the icon position (left of the text) stay as Windows draws them, and the text is drawn in RTL reading order. Measured against Windows' drawing with `RightToLeft` and `RightToLeftLayout` (`DpiDrawingTests`).
- Not mirrored yet: `TwNumberBox`, `TwDatePicker`, `TwSlider`, `TwProgress`, `TwTabStrip` (tab order), `TwDataList`, `TwTreeView`, `TwCalendar`, the `DropDownList` ComboBox painting. There are no logical spacing classes such as `ps-*`/`pe-*`; use `rtl:` instead.

## .NET 9+ color mode (`Application.SetColorMode`)

On .NET 9 and later, `Application.SetColorMode(SystemColorMode.Dark)` darkens system colors and native controls. FormWind works together with it:

- Class colors take precedence over system colors; when classes are removed, the control returns to the dark system color.
- For `dark:` variants, bind `DarkMode` to the application's color mode: `tw.DarkMode = Application.IsDarkModeEnabled;`
- What FormWind draws in place of Windows (the tab strip, ListView header text, ProgressBar colors, disabled text) does not clash with WinForms' dark drawing; tried in bound mode (both dark). Unbound (application dark, FormWind light), the parts WinForms darkens stay dark: the ListView header keeps a dark background with the light theme's dark text, and check and radio glyphs are drawn dark.
- In WinForms' own dark mode, too, the PropertyGrid's open category glyph (⌄) is drawn dark from the Explorer theme and is invisible. When FormWind gives category rows a dark background (`header:bg-*`), the glyphs are drawn as +/− in the text color (`CanShowVisualStyleGlyphs`); light mode and removing the classes restore the original glyphs.
- This behavior is verified on .NET 9 and 10 by `ColorModeTests` (run separately because it affects the whole process):

```powershell
$env:FORMWIND_COLORMODE = "1"; dotnet test tests/FormWind.Tests -f net10.0-windows --filter Category=ColorMode
```

## Krypton Toolkit

FormWind works in Krypton Toolkit applications in two ways, which can be combined.

| Krypton + FormWind (Fluent) | Krypton + FormWind (Material, dark) |
| --- | --- |
| ![Fluent](docs/krypton-fluent.png) | ![Material dark](docs/krypton-material-dark.png) |

**1. Theme bridge: no classes needed.** `TwKryptonTheme` builds a Krypton palette from the FormWind theme and makes it Krypton's global palette. Every Krypton control without classes follows the chosen preset and dark mode:

```csharp
using FormWind.Krypton;

KryptonApplier.Register();                                   // in Program.Main
var palette = TwKryptonTheme.Follow(tailwindProvider1);      // in the main form; palette.Dispose() when it closes
```

The palette is built from the theme's components: `page` → form and panel backgrounds, the form's title bar; `card` → header groups and control backgrounds; `btn-ghost` → buttons (`btn-primary` → checked `KryptonCheckButton`); `input` → text boxes, combo boxes, date and number boxes; `list` → list items; `table` → `KryptonDataGridView`; and the theme's font. It is rebuilt when the provider's theme or `TwTheme.Current` changes or `DarkMode` is toggled. It starts from Krypton's Microsoft 365 White (light) or Black dark mode (dark) palette; parts FormWind does not map (ribbon, navigator tabs, Krypton's menu rendering) keep that palette's colors. To manage a fixed palette yourself, `TwKryptonTheme.CreatePalette(theme, dark)` returns a `KryptonCustomPaletteBase`.

**2. Classes per control.** `KryptonApplier` writes classes to Krypton's state palettes: base → `StateCommon`, `hover:` → `StateTracking`, `active:` → `StatePressed`, `focus:` → `OverrideFocus` (`StateActive` for input controls), `disabled:` → `StateDisabled`, `checked:` → `StateChecked*`.

| Control | Parts |
| --- | --- |
| `KryptonButton`, `KryptonCheckButton` | `checked:` (checked states) |
| `KryptonPanel`, `KryptonLabel`, `KryptonLinkLabel` | |
| `KryptonCheckBox`, `KryptonRadioButton` | text color and font |
| `KryptonTextBox`, `KryptonMaskedTextBox`, `KryptonRichTextBox`, `KryptonNumericUpDown`, `KryptonDateTimePicker` | focus and hover → `StateActive` |
| `KryptonComboBox` | `item:` (items, `item:hover:`), `menu:` (drop-down background) |
| `KryptonGroupBox` | `caption:` (title) |
| `KryptonHeaderGroup` | `header:` (primary header) |
| `KryptonListBox`, `KryptonCheckedListBox` | `item:`, `item:hover:`, `selected:` |
| `KryptonForm` | `caption:` (title bar) |
| `KryptonDataGridView` | see below |

- On fixed-size buttons the padding is reduced until the text fits (so that Krypton does not cut the text with `…`); the `rounded-full` corner radius is limited to half the control's height (16 px in the palette).
- `KryptonDataGridView` is styled structurally (row height, grid lines, `row-header:hidden`, sort arrow) by FormWind's DataGridView applier. Because Krypton paints cells and headers with its own palette, the colors are also written to the palette: base classes and `cell:` → data cells, `header:` → column headers, `row-header:` → row headers (falling back to `header:`), `selected:` → selected cells, `bg-*` → empty area, `divide-*` → cell borders. To follow the same approach in your own applier, `TwAppliers.FindBuiltIn(control)` returns FormWind's built-in applier.
- AutoStyle does not map Krypton types by default (the bridge already styles them); add them in the theme file if you want: `"autoStyle": { "KryptonButton": "btn-ghost" }`.

**Versions.** `FormWind.Krypton` is built against Krypton Toolkit 100.25.11.328 (the package depends on that version or later) and CI tests it with both that version and 105.26.7.201. .NET Framework applications that use a newer Krypton need binding redirects; SDK-style projects generate them.

**Sample.** `samples/FormWind.Krypton.Showcase` (net8.0-windows): a form built with Krypton controls, with a preset picker. `--preset material`, `--dark`; `--snapshot output.png` takes a screenshot. In the template: `dotnet new formwind-app --Krypton true`.

## Using other control libraries

Control libraries with their own skin or theme engine (including commercial control suites) paint their controls themselves. FormWind applies its standard WinForms applier to controls it does not recognize: size, spacing and layout usually work, while colors, state variants (`hover:`, `focus:` …) and corners may conflict with the library's painting or be ignored. FormWind can be used alongside such libraries in three ways:

- **Mixed use:** The components the library is strong at (advanced grids, ribbons, schedulers …) stay on its engine; FormWind is used for standard controls, new screens and the form skeleton. The library's controls can also be items of a `TwPanel` flex/grid layout.
- **Writing an applier:** A class that implements `IStyleApplier` and is registered with `TwAppliers.Register` passes the classes to the library's own appearance objects; the base style and each variant map to the library's counterpart (normal, hovered, pressed, focused, disabled). The [Krypton Toolkit](#krypton-toolkit) integration is an example of this approach. In most skin engines, colors are applied exactly only when the skin is turned off for that control (a flat style); otherwise the skin overrides them or uses them only as a tint. For grids derived from `DataGridView`, structural styling can be left to FormWind through `TwAppliers.FindBuiltIn`; for libraries with their own grid, the applier does all of the mapping. The applier types are experimental (FW9001, see [API stability](#api-stability)).
- **Theme sync only:** No classes are given to the library's controls; when `DarkMode` changes, the library's light/dark skin is switched too, and the `tw.config.json` colors can optionally be carried into the library's palette. Each control is painted by its own engine, and both sides use the same colors.

Appliers for licensed libraries are not part of this repository; keeping them in the application's own project is recommended. The effect of FormWind on the standard controls of an existing application can be seen without touching its source code with the [harness](#trying-real-applications-harness).

## Plugins and shared processes

FormWind can also run inside another application: an application's plugin (for example a KeePass plugin) styles its own dialogs with FormWind while the host does not use it. A process can also contain several components that use FormWind. For plugin authors:

- **Give the theme to the provider:** `provider.Theme = TwTheme.FromJson(...)` (or `TwTheme.Load`). `TwTheme.Current` is process-wide; it is also the host's and the other plugins' theme, so do not change it. Providers without a `Theme` and a few parts that draw independently of a provider (`TwButton` icons, the `TwTabStrip` font, `TwIcon`, `TwToolTip`, `TwCalendar`, `TwDataList`, grid column types) use `TwTheme.Current`; they show the default theme.
- **Do not call `TwAutoTheme.Install`:** it styles every form opened on the thread, the host's included. If you need it, pick only your own forms with `Filter`. On your own form, use `ContainerControl` + `AutoStyle`.
- **Light/dark:** follow the host's state, for example in KeePass `DarkMode = KeePass.UI.UIUtil.IsDarkTheme` (KeePass has no dark theme of its own; it returns true when the system colors are dark, for example High Contrast Black).
- **What does not leak into the host:** a provider only touches its own controls and its `ContainerControl`; dark tooltips are dark only on the provider's forms; the focus ring filter and the High Contrast and text size subscriptions are per provider, and `Dispose` removes only its own registration (the other component's provider keeps working).
- **What is process-wide:** `TwAppliers.Register` (a registered applier applies to everyone using the same FormWind copy; register it for your own control type, do not override standard types) and `TwFonts.RespectTextScale`.
- **Dependencies (.NET Framework):** put `FormWind.dll`, `System.Text.Json.dll` and its dependencies (`System.Memory`, `System.Buffers`, `System.Runtime.CompilerServices.Unsafe`, `System.Text.Encodings.Web`, `System.Numerics.Vectors`, `System.Threading.Tasks.Extensions`, `Microsoft.Bcl.AsyncInterfaces`, `System.ValueTuple`) in the plugin folder. The host's `.exe.config` has no binding redirects for them, so these packages ask for older versions of each other (System.Memory → Unsafe 4.0.4.1, file 6.0.0.0); FormWind redirects these requests with `AppDomain.AssemblyResolve` to the file in its own folder with the same public key and at least the requested version (what a binding redirect does). If another component already loaded a suitable version, that one is used. If the plugin's own code uses these assemblies directly (for example its own `JsonSerializer` call), the same applies to it; touching a FormWind type (`TwTheme`) before the first use installs the resolver.
- **Two FormWind versions side by side:** the assemblies are strong-named (`PublicKeyToken=1cea47bdc82de2bf`); two copies with different `AssemblyVersion`s (for example 0.91 and 0.92) load side by side with `LoadFrom` and have separate statics: theme, appliers and hooks do not see each other. Two copies of the same version load as one assembly and share statics.

Tried with KeePass 2.61: two plugins (FormWind 0.91 and 0.92) opened dark dialogs with their own colors in the same process, KeePass's main window kept its colors and fonts, and `TwTheme.Current` stayed the default.

## DPI

Recommended setup: `AutoScaleMode = AutoScaleMode.Dpi` and `AutoScaleDimensions = new SizeF(96F, 96F)` on forms (for forms built in code, wrap the constructor in `SuspendLayout`/`ResumeLayout`), and run the application as `PerMonitorV2`. `samples/FormWind.Showcase/ControlsForm.cs` is set up this way; at 125% (120 DPI) the content scales and fits. Measurements:

| Setup | Result |
| --- | --- |
| DPI-unaware (the 4.7.2 default) | Windows scales the bitmap; the layout is correct, the text blurry. |
| PerMonitorV2 + `AutoScaleMode.Dpi` | Layout, fonts and FormWind spacing scale together. |
| PerMonitorV2 + `AutoScaleMode.Font` | Forms built in code and (on .NET Framework) Designer forms whose `Font` is assigned after `AutoScaleDimensions` did not scale; spacing and fonts grew, controls did not. |

### Setup on .NET Framework

On .NET 8+, `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` or `Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)` in `Program.Main` is enough. On .NET Framework 4.7.2 / 4.8 there are three options:

| Option | How to turn it on | Result |
| --- | --- | --- |
| DPI-unaware (the default) | No setting | Windows scales the window as a bitmap; the layout is correct, the text blurry. `DeviceDpi` stays 96 and FormWind does not scale. |
| System-aware | `<dpiAware>true</dpiAware>` in the manifest | Crisp at the primary screen's scale. When the window moves to a screen with another scale, Windows stretches or shrinks it as a bitmap. |
| PerMonitorV2 (recommended) | The manifest **and** App.config, both | Crisp on every screen; WinForms and FormWind rescale when the screen changes. |

For PerMonitorV2 the manifest makes the process DPI-aware and declares Windows 10 compatibility (without `supportedOS` the process silently stays system-aware):

```xml
<compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
  <application>
    <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
  </application>
</compatibility>
<application xmlns="urn:schemas-microsoft-com:asm.v3">
  <windowsSettings>
    <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
    <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
  </windowsSettings>
</application>
```

App.config turns on WinForms' own per-monitor support:

```xml
<configuration>
  <System.Windows.Forms.ApplicationConfigurationSection>
    <add key="DpiAwareness" value="PerMonitorV2" />
  </System.Windows.Forms.ApplicationConfigurationSection>
</configuration>
```

- **Both are needed.** With PerMonitorV2 only in the manifest the process runs per-monitor, but WinForms does not handle screen changes (`WM_DPICHANGED`): a form moved to a screen with another scale keeps the layout and fonts of the old DPI. On a single screen there is no visible difference. If a single screen is enough, `<dpiAware>true</dpiAware>` (system-aware) alone in the manifest is more consistent.
- **Do not add `configSections`.** `System.Windows.Forms.ApplicationConfigurationSection` is already declared in machine.config; declaring it again with `<configSections>` in App.config makes WinForms silently ignore the DPI setting and the configuration system fail to start. The error does not appear at startup but on the first screen that uses `ConfigurationManager` (settings, connection strings), as "Configuration system failed to initialize".
- **Do not remove the `bindingRedirect`s.** On .NET Framework the FormWind package turns on `AutoGenerateBindingRedirects` and the build writes the redirects to the output `.exe.config`. If you remove the redirects the build generated while editing this file by hand, the `System.Text.Json` dependencies cannot be loaded and the application crashes at startup.
- **Template:** `dotnet new formwind-app --Framework net472` sets up the manifest and App.config this way.

**Applications with their own DPI scaling.** An application that enlarged sizes with its own factor (such as `Px(8)`, `Fs(13)`) while running DPI-unaware scales the same value twice once it becomes DPI-aware: first with the application's factor, then with WinForms' AutoScale or FormWind (about 1.56× instead of 1.25× at 125%). When switching:

- Write class values in 96 DPI units: `text-[13px]`, `p-[8px]`. Pre-scaled values such as `$"text-[{Fs(13)}px]"` are doubled by FormWind's scaling.
- Use `AutoScaleMode.Dpi` and `AutoScaleDimensions = new SizeF(96F, 96F)` on forms; write `Size`, `Location`, `Padding` and similar values assigned in the constructor (before `ResumeLayout`) in 96 DPI units too, AutoScale enlarges them.
- For pixel values assigned in code after the form opens, use `control.LogicalToDeviceUnits(8)` instead of your own factor; in your own `OnPaint` drawing the scale is `DeviceDpi / 96f`.
- Open the application at 125% and 150% and check: if spacing and text grew clearly more than the screen scale, a value is still scaled twice.

- For how to turn on PerMonitorV2 on .NET Framework, see [Setup on .NET Framework](#setup-on-net-framework).
- On .NET Framework, WinForms applies its DPI setting when the first `Control` is created, while GDI+ reads the screen DPI once when it starts. If a `Font` is created before that (e.g. while loading the theme), GDI+ stays at 96 DPI and native controls (TextBox) draw their fonts too small. FormWind triggers WinForms' DPI initialization before touching GDI+; if your own code creates a `Font` in `Program.Main` before creating a form, call `new Control().Dispose()` first.
- **Moving between screens (150%, 200%):** when a form moves to a screen with a different DPI, FormWind reapplies the classes once after WinForms finishes scaling. On forms set up with `AutoScaleMode.Dpi`, fonts, spacing, owner-drawn controls and DataGridView (fonts and column widths) scale correctly; this was verified at 150% and 200% for `ControlsForm` and a grid form.
- **`AutoScaleMode.Font` and .NET Framework:** the Designer writes the form's `Font` after `AutoScaleDimensions`. With PerMonitorV2, .NET Framework WinForms skips scaling on that font assignment and updates `AutoScaleDimensions` as if it had scaled: controls keep their 96 DPI size while fonts (and FormWind spacing) grow with the screen DPI; text is clipped and grid columns stay narrow. This also happens without FormWind (the same form scales correctly when `Font` is assigned first) and does not happen on .NET 8. It cannot be fixed at run time: the overwritten value no longer tells at which DPI the form was designed. The **FW002** analyzer warns about these forms; the fix is `AutoScaleMode.Dpi` with `AutoScaleDimensions = new SizeF(96F, 96F)`.
- In Font-mode forms .NET Framework changes fonts without raising events when moving between screens; the inner box of `TwTextBox` kept a deleted font handle and drew its text small with the system font. `TwTextBox` now refreshes the inner font on the form's `DpiChanged` event (`Text_boxes_keep_a_valid_native_font_after_moving_to_200_percent`).
- **Text drawn in place of Windows:** text FormWind draws after Windows (disabled label, check box, radio button and flat button text, the tab strip) follows WinForms' layout exactly: disabled text is drawn by a hidden, enabled label with the same font and format and copied into place (with the control's `UseCompatibleTextRendering`: GDI or GDI+). Check boxes and radio buttons take the glyph size from the theme at the window's DPI. Tab text is drawn without padding in Windows' layout (icon, `Padding.X` gap, text; centered together); before, the text Windows fitted was shortened to "Gen…" at 200% and disabled labels moved by 2–3 px. Measured at 125%, 150% and 200% (with WM_DPICHANGED) and on a real 120 → 96 DPI screen change; `DpiDrawingTests` checks the same layout at 9, 13.5 and 18 pt.
- Tests: the `Controls_*_at_150/200_percent` visual tests open the showcase in a separate PerMonitorV2 process and scale it with WM_DPICHANGED (`FormWind.Showcase.exe --snapshot x.png --controls --dpi 192`; only effective with the PerMonitorV2 configuration). The baseline name includes the process's system DPI.

Pixel values are scaled by the control's `DeviceDpi / 96`; values in classes are 96 DPI units (`p-2` is 10 px at 125%). On controls the application itself does not scale for DPI (e.g. UserControls whose AutoScale does not work), this gives different results than the original values.

- **The form's AutoScale:** `SetClasses` in `InitializeComponent` assigns the scaled value; the form's AutoScale (the final `ResumeLayout`) would then scale the same padding again (8 → 10 → 12 px at 125%). In nested UserControls with their own `AutoScaleMode` this scaling also happens after the handle is created. FormWind prevents this in two ways:
  - If the classes define padding or margin, FormWind's value is written back when it changes (`PaddingChanged`/`MarginChanged`).
  - The classes are reapplied once when the control's first handle is created (after scaling, before `Load`); size and font are corrected too.
- **Consequence:** as long as the classes manage padding/margin, run-time assignments to these properties by application code are undone (FW001 warns about them); to keep the application's value, remove padding/margin from the classes. Tracking stops when the classes are removed. Colors work the other way round: the application's assignment [is kept](#colors-assigned-by-application-code). No tracking happens in the Designer. When moving between screens, WinForms' scaling is undone for a moment and the DPI refresh then applies the new scale.
- Verification: compared with the original application at 125% on the converted Git Extensions forms; the remaining differences come from 1 px rounding and from controls the application never scales.
- Scaled pixels are rounded like WinForms does (.5 up).

If the application runs DPI-aware (e.g. `PerMonitorV2`) but the forms' AutoScale does not enlarge the layout, spacing grows while controls do not, and text does not fit. In that case fix AutoScale or set `TailwindProvider.ScaleWithDpi = false`. In DPI-unaware applications (the default for most 4.7.2 projects) Windows does the scaling and no extra setup is needed.

## Designer

- `TwTabControl`: "Add page" / "Remove selected page" commands and a smart tag (style selection); clicking a tab at design time switches the page; only `TwTabPage`s can be dropped onto the control itself, other controls are dropped onto the page. A control dropped from the Toolbox starts with two pages.
- **Smart tag:** while the form has a `TailwindProvider`, every control's smart tag (standard controls included) shows a FormWind section: the `Classes` field, a `Component` list, components suited to the control type (button → `btn-primary`/`btn-ghost`/`btn-danger`, text box → `input`, panel → `card`, grid → `table`; when defined in the theme) and "Clear classes". Choosing a component only replaces the current component and keeps the other classes (`btn-ghost px-6` → `btn-primary px-6`). Changes go through the `Classes` property, so they are written to Designer.cs and can be undone.
- **Class builder:** sections of the smart tag hold drop-downs grouped by kind: colors (background, text, border), spacing and shape (padding, border, corners), typography (size, weight), hover and dark mode (`hover:bg-*`, `dark:bg-*`, `dark:text-*`). Each field reads and changes only the classes of its own group; choosing `px-4` removes the classes it overrides, such as `px-2` and `pl-1`, and keeps `py-2`. Clearing a field removes that group's classes. Unrecognized classes are listed as a warning in the smart tag. Color lists start with the theme's own colors (`brand-500` …); free text can be typed too.
- **Preview and Designer.cs:** the only FormWind lines written to Designer.cs are `SetClasses` calls. Properties managed by the classes (BackColor, ForeColor, Font, Padding, Margin, Cursor) and the preview's side effects (flat button: `FlatStyle`, `FlatAppearance`, `UseVisualStyleBackColor`; the `BorderStyle` of a text box with a painted frame; grid styles; link colors) are not written: while a control is serialized the preview is taken off and applied again afterwards. No `ComponentChanged` is raised while saving, so the form does not look modified right after it is saved. Values set by hand that the classes do not manage are still written. Turning `DesignTimePreview` off puts the controls' original values back. Saving and reopening does not change the code; at 150 % `h-10` is scaled once (60 px) and does not grow. The tests use Visual Studio's CodeDOM serializer (`DesignerCodeTests`).
- The `TwTabControl` designer is built for .NET Framework and .NET 8; in the .NET 8 out-of-process Designer it runs in the server process (the commands are verified on both targets with `DesignSurface` tests).
- **.NET 8+ (out-of-process) Designer:** the smart tag, the class builder, the drop-downs and the preview work in both Designers (they are all built on `TypeConverter` properties and a `DesignerActionList`). Only the "…" window of the `Classes` field (`ClassesEditor`: multi-line editing) is limited to the .NET Framework Designer. On .NET 8 the `Classes` field is a drop-down that suggests the theme's components (`ClassesConverter`); the list shows the value with the current component replaced (`btn-primary px-6`), and free text can be typed too.
- **Theme file:** the package's `build/FormWind.targets` embeds `tw.config.json` from the project folder into the application's assembly. The Designer finds the assembly of the form being designed and reads `tw.config.json` from its folder (with fonts and SVG icons), otherwise the embedded copy, so the preview and the component and color lists use the project's theme. After changing the theme file, build the project and reopen the form. Before the project's first build the default theme is used. For a file elsewhere set `<FormWindThemeFile>path/tw.config.json</FormWindThemeFile>`; to turn the embedding off set `<FormWindEmbedTheme>false</FormWindEmbedTheme>`. At run time the application still loads the file with `TwTheme.LoadIfExists`; only the Designer reads the embedded copy. The file is also copied to the output and publish folders as `tw.config.json`; if the project already copies it, it is not added again, and `<FormWindCopyTheme>false</FormWindCopyTheme>` turns the copying off. When several projects with different themes are open in the same Visual Studio process, drawing that does not go through a provider (icons, grid columns) uses the theme of the form opened last.

### FW001 analyzer

The Roslyn analyzer shipped with the package warns when code assigns a property that classes manage:

```csharp
tw.SetClasses(btnSave, "btn-primary");
btnSave.BackColor = Color.Red;   // FW001: 'btnSave' has classes; assigning 'BackColor' conflicts with them. …
```

At run time a color assignment wins: FormWind leaves that color channel to the application (see [Colors assigned by application code](#colors-assigned-by-application-code)); the warning can be suppressed for deliberate assignments such as status colors. Padding and margin assignments, on the other hand, are undone and the class wins (see [DPI](#dpi)); here the warning points to a real conflict.

Only calls with constant class strings and properties the classes actually manage are checked (`bg-*` → BackColor, `text-{color}` → ForeColor, `text-{size}` → Font, `p-*` → Padding, `m-*` → Margin, `cursor-*` → Cursor; default components included). A plain `Font` assignment does not warn when only `font-sans` or `font-semibold` is present. To turn it off: `dotnet_diagnostic.FW001.severity = none` in `.editorconfig`.

### FW002 analyzer

In .NET Framework projects, warns when an `InitializeComponent` that uses `SetClasses` sets `AutoScaleMode.Font` and assigns the form's `Font` after `AutoScaleDimensions` (see [DPI](#dpi)): with PerMonitorV2 the form does not scale. The target framework is taken from `build_property.TargetFramework`, or else from the referenced WinForms version. If the application does not run PerMonitorV2, the warning can be turned off with `dotnet_diagnostic.FW002.severity = none` in `.editorconfig`.

## Animations and responsive variants

- The tab indicator (underline, pill, segment) slides to the new tab; list row hover backgrounds fade in; skeleton rows (list and grid) pulse gently; the `TwSwitch` knob slides, `TwToast` fades in and out, `TwDialog` and `TwPopover` fade in when they open, and the indeterminate `TwProgress` segment slides along the track. The switch knob, the tab indicator and the list hover follow the control's `duration-*`, `delay-*` and `ease-*` classes; `transition-none` makes them immediate.
- **`animate-spin`** turns a `TwIcon` (one turn per second; loading indicators), **`animate-pulse`** fades a `TwIcon` or the background of a `TwPanel` in and out (skeleton placeholders), `animate-none` stops them. All animated controls share one clock, so pulsing placeholders pulse together.

```csharp
tw.SetClasses(icoLoading, "text-gray-500 animate-spin motion-reduce:animate-none");
tw.SetClasses(pnlSkeleton, "h-4 w-40 rounded bg-gray-200 animate-pulse");
```

- `TwMotion.Enabled` follows Windows' "Show animations" setting (also when it changes while the application runs) and can be turned off in code; `TwMotion.FollowSystem()` hands it back to Windows. When it is off all of these and `transition` changes happen immediately (the indeterminate progress segment stands still in the middle, a spinning icon stops). There is no animation at design time or on invisible controls.
- **`motion-safe:`** / **`motion-reduce:`** variants apply while animations are on / off; when the setting changes only the controls that use them are reapplied. `forced-colors:` still applies last.
- **Closing:** `TwDialog` and `TwPopover` also fade when they close. The window closes at once (`DialogResult`, `IsOpen`, `Closed` are not delayed); its last picture fades out in a click-through window.
- **Values:** a new `TwProgress.Value` slides the fill to its place (300 ms; `duration-*`, `ease-*`, `transition-none`). `TwStat` counts a new value up from the previous one (from zero when it first appears), keeping the text's form (`$12,400`, `98.4%`); only a value with a single number is counted, `AnimateValue = false` turns it off. Charts open the first time they are shown with data: `TwChart` bars grow from the baseline one after another (35 ms apart) and lines and areas draw from left to right; `TwRingChart` rings fill from zero; `TwHeatmap` cells fade in from the top left to the bottom right; `TwTimeline` dots pop up in time order with a spring and the joining line extends; a `TwSparkline` line draws from left to right and its bars grow. The delays add up to 400 ms at most. When the data changes after that, the chart is not redrawn from scratch: the bars, points, fills and shades on screen move to the new ones in 350 ms (also through calls that clear and re-add, such as `SetData`). `AnimateOnShow = false` turns the opening off, `AnimateChanges = false` the move; `AnimateIn()` plays the opening again (e.g. after loading new data).
- **Collapsing panel:** `TwPanel.Expanded = false` (or `Collapse()`) slides the panel's height down to zero and hides it, `Expand()` shows it and slides it back to its height (its preferred height when `AutoSize`); 200 ms, changed with `duration-*` / `ease-*`. The children keep their places and are cut off while it slides. The height must be the panel's own: `Dock = Top`/`Bottom` or not docked (a panel that grows with `grow` in a flex container does not slide).

```csharp
btnFilters.Click += (_, _) => pnlFilters.Expanded = !pnlFilters.Expanded;
```

- For animations in your own control under the same rules, use `TwTween`: call `Start(duration, easing, delay)` when the value changes and read `Progress` (0–1) in `OnPaint`; the owner is invalidated every frame, and when animations are off `Progress` is 1 right away. The `Tick` (every frame) and `Completed` events are for driving properties such as opacity. `TwMotion.Ease`, `Lerp` and `Blend` give the same curves and interpolation.

```csharp
_tween = new TwTween(this);
void Expand() { _from = _height; _tween.Start(200, TwEasing.InOut); }
// OnPaint: float h = _from + (_target - _from) * _tween.Progress;
```

- Curves: `TwEasing.Out` (default), `In`, `InOut`, `Linear` and `Spring` (a damped spring that goes ~10% past the target and settles back; `Progress` goes above 1 for a moment, `Blend` clamps colors). For a custom curve, as in CSS, `new TwCubicBezier(x1, y1, x2, y2)`: `_tween.Start(250, new TwCubicBezier(0.34, 1.56, 0.64, 1))`.
- Standard durations are in `TwMotion.Durations`: `Xs` 120, `S` 180, `M` 250, `L` 450 ms. When animations are off they are all 1 ms, so anything that waits for them ends at once.
- To animate a number of a control (a width, a position, an opacity) without writing `OnPaint`, use `TwAnimator.Animate(control, name, from, to, duration, set)`; a new call with the same control and name replaces the running one (to continue from where it is, pass the last value given to `set` as `from`). Overloads take a curve, a delay and `completed`; `Cancel` and `IsAnimating` take the same name. When animations are off the value is set to `to` at once.

```csharp
TwAnimator.Animate(pnlSide, "width", pnlSide.Width, 320, TwMotion.Durations.M, w => pnlSide.Width = (int)w, TwEasing.Spring);
```

- **Notifications:** `TwToast.Position` picks the corner (`BottomRight` by default, `TopRight`, `BottomLeft`, `TopLeft`, `BottomCenter`, `TopCenter`; at the top the newest is on top). `TwToast.MaxVisible = 1` puts each new notification in the place of the previous one (which fades out where it is; 0 = no limit). A new notification slides 12 px in from its edge (`TwToast.EnterEasing`, e.g. `TwEasing.Spring`); a success notification draws its check mark. `toast.Subtitle` adds a muted line under the message ("DR-24817 · Line 2") and rearranges the stack.
- **Busy button:** `TwButton.IsBusy = true` draws a spinner before the text and ignores clicks (Enter, Space, `PerformClick`, `AcceptButton`); no `Click` and no `DialogResult`, and screen readers hear the busy state.

```csharp
btnSave.IsBusy = true;
try { await SaveAsync(); } finally { btnSave.IsBusy = false; }
```

- **Popovers and menus:** `TwPopover` and the menus FormWind draws grow from 95% and fade in from the anchor's side (the owner item for a submenu, the cursor for a context menu) in 140 ms, and fade out in 120 ms when they close. A window's content cannot be scaled, so during the opening the real window is invisible (it already takes clicks and keys) and a click-through picture of it grows in its place.
- **Focus ring:** a ring that appears for keyboard focus (`focus-visible:ring-*`, `focus:ring-*`) starts 6 px outside and closes in onto the control in 180 ms; for mouse focus it appears in place.
- **Grid loading:** `TwGrid.SetLoading(grid, true, TwLoadingStyle.Shimmer)` sweeps a light band across the skeleton bars from left to right (the default `Pulse` fades them; `SetLoadingStyle` changes it later).
- **Transitions:** `TwTransition.CrossFade(host, change)` first puts a picture of `host` over it, applies `change` and fades the old picture away in 250 ms. For page and theme switches:

```csharp
TwTransition.CrossFade(pnlPage, () => ShowPage(next));
TwTransition.CrossFade(this, () => provider.DarkMode = !provider.DarkMode);   // the whole form, title bar included
```

- `sm:`, `md:`, `lg:`, `xl:`, `2xl:` variants apply based on the form's client width (640 / 768 / 1024 / 1280 / 1536 px at 96 DPI; configurable via `TwBreakpoints`). When the form is resized and a threshold changes, only controls using these variants are restyled:

```csharp
tw.SetClasses(pnlForm, "flex flex-col gap-4 md:flex-row lg:gap-8");
tw.SetClasses(lblTitle, "text-lg lg:text-2xl");
```

## Coding agents (MCP)

[`website/llms.txt`](website/llms.txt) summarizes FormWind for coding agents. The `formwind-mcp` server ([`tools/FormWind.Mcp`](tools/FormWind.Mcp/README.md)) lets an agent check its work: `validate_classes` (unknown classes with suggestions), `explain_classes` (the values each state resolves to), `list_components`, `list_vocabulary` and `render` (a PNG of the control in light and dark mode, per state). It is read-only and runs no application code.

```bash
dotnet tool install -g FormWind.Mcp --prerelease
```

```bash
claude mcp add formwind -- formwind-mcp
```

The server uses `tw.config.json` in the folder the agent starts it in; every tool also takes `preset` and `themeFile`.

## Project structure

```
src/FormWind/             Core: parser, theme, state engine, WinFormsApplier, Tw* controls, Designer editor
src/FormWind.Krypton/     KryptonApplier, TwKryptonTheme
src/FormWind.Analyzers/   FW001 Roslyn analyzer (included in the FormWind package)
tests/FormWind.Tests/     xUnit tests (net472, net8.0, net9.0, net10.0-windows)
samples/FormWind.Showcase/ Showcase application (net472)
samples/FormWind.Krypton.Showcase/ Sample with Krypton Toolkit (net8.0-windows)
tools/FormWind.Convert/   Command-line tool that converts Designer files to classes (net8.0-windows)
tools/FormWind.Harness/   Startup hook and harness for trying FormWind on real applications (net8.0-windows)
tools/FormWind.Docs/      Generates the class reference site with previews (net8.0-windows)
tools/FormWind.Mcp/       MCP server for coding agents: validates, explains and renders classes (net8.0-windows)
website/                  Landing page and documentation site in English and Turkish (static HTML; website/build.ps1)
templates/                dotnet new template (formwind-app)
tools/run-harness.ps1     Runs a .NET WinForms application with the harness and prints a log summary
.github/workflows/ci.yml  Build, test, pack and upload of visual snapshots as artifacts
```

## Development

Contribution guidelines, test categories, public API and versioning policy: [CONTRIBUTING.md](CONTRIBUTING.md). How the code is structured (the path from a class string to the screen, modules, design decisions): [ARCHITECTURE.md](ARCHITECTURE.md). Changes: [CHANGELOG.md](CHANGELOG.md). Security issues: [SECURITY.md](SECURITY.md).

All CI steps (build, tests on four targets, coverage check, packing) run with a single command; `-Quick` only builds and tests on .NET 8. The SDK version is pinned in `global.json` (.NET 10 SDK):

```powershell
.\build.ps1
```

Individual commands:

```bash
dotnet build FormWind.sln
```

```bash
dotnet test FormWind.sln
```

Performance and GDI tests (`Category=Performance`) are skipped in the default run. They take ~6 minutes in total (the long-running test ~5 minutes) and do not run in parallel with other tests because they measure process-wide GDI counters. To run them and see the measurements, set the `FORMWIND_PERF=1` environment variable (PowerShell):

**Long-running use (`SoakTests`).** A form full of controls that FormWind subclasses or hooks is opened again and again, the theme is switched, the application sets its own theme (`SetWindowTheme`), handles are recreated (`RightToLeft`, `Multiline`) and the form is closed. Controls: dark ListView and TreeView, RichTextBox loaded with RTF, DropDownList, DateTimePicker, disabled ListView and ComboBox, tooltips, PropertyGrid and grid. A provider attached by hand and `TwAutoTheme` are tried separately. Over 300 rounds (net9) GDI stays 62→62, USER 37→37, managed memory stays at ~4.3 MB and a round takes as long in the second half as in the first; every form and provider except the last round's is collected. The default run includes an 8-round short version. The run showed that the handles of closed tooltip windows were never forgotten; they are now removed when the window is destroyed.

**Large forms (`LargeFormTests`, net9).** Tabs, controls nested in panels (label, text box, button, check box, drop-down list) and a grid or list every 100 controls:

| Controls | First styling (AutoStyle / classes by hand) | Theme switch + paint (AutoStyle / classes by hand) | FormWind memory |
| ---: | --- | --- | --- |
| 1,011 | 165 ms / 362 ms | 38 ms / 38 ms | 2.0–3.3 KB/control |
| 2,523 | 447 ms / 1,160 ms | 39 ms / 83 ms | 2.3–2.4 KB/control |
| 5,039 | 824 ms / 1,550 ms | 85 ms / 118 ms | 2.1–2.5 KB/control |

Time grows linearly with the number of controls (time per control stays flat). With AutoStyle on, adding controls later grows from 250 to 2000 controls at the same rate as WinForms itself (one by one ~9x, WinForms ~12x; WinForms lays out the parent again on every add). Use `SuspendLayout`/`ResumeLayout` for bulk adds. While FormWind styles a control it suspends the parent's layout and runs it once; before, each font, padding and size assignment triggered its own layout (2000 controls: 5.8 s → ~4.1 s). At ~60 theme switches per second color transitions (`transition`) overlap, every control ends on its target color and the handle count does not grow; with 200 animated controls the frame rate drops to ~10 fps (mostly the corner and border painting of cards). Thresholds are not milliseconds but time per control and growth ratios (time per control at 5000 controls stays under twice that at 1000; growth when adding later stays under 1.5 times WinForms').

```powershell
$env:FORMWIND_PERF = "1"; dotnet test FormWind.sln --filter Category=Performance --logger "console;verbosity=detailed"
```

To run them from the Visual Studio Test Explorer, define the variable system-wide before opening Visual Studio.

### Public API

`src/FormWind/PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` list the library's public surface. The build warns when a public member is added that is not in the list, or a listed member is removed (RS0016/RS0017); new members go into the `Unshipped` file and move to `Shipped` when a version is released. Bulk add with the code fix:

```bash
dotnet format analyzers src/FormWind/FormWind.csproj --diagnostics RS0016 --severity warn
```

`src/FormWind.Krypton` has its own pair of files. Only helpers used by the library itself (drawing, parser, internal appliers, Designer types) are `internal`; extension points (`IStyleApplier`, `TwApplyContext`, `TwStyle`, `ITwChrome`) are public, and those that may still change are marked experimental (see [API stability](#api-stability)).

### Code coverage

Coverage is measured with the "Code Coverage" collector that ships with the test SDK (net472 and net8.0-windows; only `FormWind` and `FormWind.Analyzers`):

```powershell
dotnet test tests/FormWind.Tests -f net8.0-windows --settings tests/coverage.runsettings --collect "Code Coverage" --results-directory TestResults/net8.0-windows
```

```powershell
.\tools\check-coverage.ps1 -Results TestResults -Minimum 0.80
```

CI writes both targets' reports to the workflow summary as a table and fails if `FormWind` line coverage drops below 80% (currently ~85–87%). Coverlet is not used because it cannot resolve the WinForms framework assemblies.

### Invariant tests

`InvariantTests` applies a random sequence of operations to a random control (16 types: standard controls, ToolStrip, DataGridView, `Tw*` controls): assigning classes, dark mode, disabling, hover, changing the parent's color, application color assignments, recreating the handle. After every step it checks that the colors equal the resolved style (or the color the application assigned), and at the end that the original values return when the classes are removed.

The default run executes 300 scenarios. Scenarios are seeded; a failure message prints the seed and the operation sequence:

```powershell
$env:FORMWIND_INVARIANT_RUNS = "3000"; dotnet test tests/FormWind.Tests --filter FullyQualifiedName~InvariantTests
```

```powershell
$env:FORMWIND_INVARIANT_SEED = "162"; dotnet test tests/FormWind.Tests --filter FullyQualifiedName~InvariantTests
```

For every bug these tests found, `ApplierTests` also keeps an explicit regression test; seeds may not reproduce the same scenario if the generator changes.

### Trying real applications (harness)

`tools/run-harness.ps1` runs a .NET (Core) WinForms application with FormWind **without changing its source code**. `FormWind.Harness`, loaded via `DOTNET_STARTUP_HOOKS`, applies classes to every opened form by control type (`btn-ghost` on buttons, light/dark colors on text boxes, strip colors on ToolStrips …) and records the results:

```powershell
.\tools\run-harness.ps1 -Exe C:\apps\MyApp.exe -SelfTest -ExitAfter 60
```

| Parameter / environment variable | Effect |
| --- | --- |
| `-Dark` / `FORMWIND_DARK=1` | Starts in dark mode. **Ctrl+Alt+Shift+D** toggles the theme while running. |
| `-SelfTest` / `FORMWIND_SELFTEST=1` | Captures every new form in light and dark mode and audits its contrast both from the class colors and from the image. |
| `-ExitAfter 60` / `FORMWIND_EXIT_AFTER` | Ends the process after the given number of seconds (unattended runs). The countdown runs on a background thread, so the process ends even when UI timers stop. |
| `-Skip DataGridView,ToolStrip` / `FORMWIND_SKIP` | Does not style these types; to narrow a problem down to one control type. |
| `FORMWIND_DISABLE=1` | Turns the harness off; to compare the same scenario without FormWind. |
| `-Dump` / `FORMWIND_DUMP=1` | Applies no styles; writes the color, font and padding values of each form's controls to `dump-<form>.txt`. Run before and after the conversion tool and compare. |
| `FORMWIND_CLOSE=FormSettings` | Closes the named forms after processing them; to reach forms behind modal windows shown at startup without supervision. |
| `FORMWIND_WALK=Launch` | Clicks the buttons with this text on the first form one by one; processes and closes each form that opens and moves on; exits the process when done (e.g. every sample of a demo application). `FORMWIND_WALK_WAIT` is the wait per form (ms, default 4000), `FORMWIND_WALK_SKIP=33,40` the positions of buttons to skip. |
| `FORMWIND_MENU=m_menuEntryAdd,m_menuToolsOptions` | Clicks the menu items with these names (`ToolStripItem.Name`) on the first form one by one; processes and closes each form that opens (waits `FORMWIND_WALK_WAIT`). For reaching dialogs opened from menus in applications that `SendKeys` cannot reach because their window cannot come to the foreground (KeePass: entry, options, password generator, database settings). Works with `-Dump` too. |
| `FORMWIND_OPEN=SettingsForm,AboutForm` | Finds these form types in the loaded assemblies and opens, processes and closes them one by one with their parameterless constructor; to reach forms opened from menus or the tray without supervision (reflection for testing). Forms that refuse to close are disposed. |
| `-Skip '*'` / `FORMWIND_SKIP=*` | Styles no controls but still measures; to compare the same scenario without FormWind (stalls, paint counts, application errors). |
| `FORMWIND_SELFTEST_DELAY=30000` | Waits before the self-test's first capture (ms, default 1500); for applications that load for a long time at startup and disable their controls meanwhile (Bulk Crap Uninstaller). |
| `-Dpi 144` / `FORMWIND_DPI=144` | Scales every form, after styling it, as if moved to a screen with this DPI (WM_DPICHANGED; only effective in PerMonitorV2 applications, logged as `DPI MainForm 120 -> 144`). `screen` moves the form to a real screen with another DPI. In the simulation only the form gets the new DPI; parts Windows measures itself (ListView group headers, check glyphs, PropertyGrid toolbar icons) stay at the screen's DPI — compare with a run without FormWind (`-Skip '*'`). |

Logs and images are written to `%TEMP%\FormWind-Harness\<process>-<time>\`:

| Line | Meaning |
| --- | --- |
| `FORM` | Form, number of styled controls and duration; `unstyled` lists unrecognized control types |
| `ADDED` | Controls added and styled later |
| `CONTRAST` | Pairs found by the contrast audit (light and dark mode) |
| `SCREEN` | Text below 3:1 in a self-test image: within the control's area the most frequent color counts as the background and the pixel with the highest contrast against it as the text. Catches what the class colors cannot show (disabled text drawn by Windows, controls painting with their own palette); the measurement is approximate because of font smoothing |
| `OWNERDRAW` | Controls that paint their own colors (`TwContrast.FindOwnerDrawn`): the contrast audit cannot measure them and the classes' cell/item colors may be ignored; check them by eye in dark mode |
| `UI STALL` | The UI thread froze for more than 250 ms |
| `PAINT STORM` | More than 100 paints per second for 3 seconds in a row: an invalidate-while-painting loop |
| `TIMER STARVATION` | The UI thread processes messages but WinForms timers have not ticked for 2 seconds: Windows only generates `WM_TIMER` when no other message is queued, so a window that invalidates itself in every `OnPaint` (e.g. ShareX's region capture) holds it off indefinitely. Meanwhile the harness cannot find new forms and `PAINT STORM` cannot be measured; `TIMER STARVATION ended` is logged when timers resume |
| `UI BLOCKED` | The UI thread has not processed any message for 2 seconds; its duration is also logged as `UI STALL` when it ends |
| `EXCEPTION`, `UNKNOWN` | An error while applying styles; an unknown class |

- Startup hooks only exist in .NET (Core) applications; they are not loaded in trimmed applications or when hooks are disabled. For .NET Framework applications, add `FormWindHarness.cs` from `FormWind.Harness` (`LangVersion` `latest`) and FormWind's net472 build to the project and call `FormWindHarness.Install()` at the start of `Main`. Without a message loop `Install` creates no window (the application's later `SetCompatibleTextRenderingDefault` call would fail); the harness is installed when the first form opens. Turn on `AutoGenerateBindingRedirects` in the project for FormWind's dependencies.
- In an application that already uses FormWind (its form has a field of type `TailwindProvider`, e.g. the `formwind-app` template, or the form is styled by `TwAutoTheme`), the harness does not restyle that form: it uses the application's provider, audits its classes and switches the theme through it. Before, the harness's classes overrode the application's flex layout and produced false contrast issues.
- In applications that open windows on separate UI threads (Open Live Writer opens every editor and the splash screen on its own), the harness is installed on each thread: each thread scans its own forms, stalls and timer starvation are measured per thread, and the theme shortcut reaches all of them.
- When the application's error dialog (`ThreadExceptionDialog`) opens, the first lines of the error are logged under the `APP EXCEPTION` line.
- The harness only descends into containers that carry content (forms, panels, GroupBox, tabs, SplitContainer, UserControl, ToolStrip containers). The inner parts of composite controls (e.g. the `TextBox` inside a `KryptonTextBox`, the help pane of a `PropertyGrid`, the edit box of a `NumericUpDown`) belong to the control itself and are not styled.
- Self-test images of top-level windows are taken with `PrintWindow`: the title bar, `RichTextBox` and MDI children look as on screen, and it works even when the window is covered by another one. MDI children (where `PrintWindow` also captures sibling windows on top) and failed captures fall back to `DrawToBitmap`, where the title bar does not look as Windows draws it and `RichTextBox` is drawn incorrectly. `PrintWindow` returns forms with `RightToLeftLayout` mirrored; the image is flipped back as on screen, otherwise text reads backwards and the `SCREEN` audit measured controls in the wrong place.
- Forms are found with a timer, so this works whether the application runs a WinForms or a WPF message loop (e.g. Greenshot). Classes are applied after a form is shown, so it may appear unstyled for a moment at startup; colors the application assigned at that point count as original values.
- When you find a problem, first reproduce it without FormWind using `FORMWIND_DISABLE=1`; if it still happens, it belongs to the application. Then narrow it down to one control type with `-Skip`.
- Applications with their own theme engine often write the parent's background into every child explicitly (e.g. ShareX). Before a form is styled, the harness resets `BackColor` on children whose background equals their parent's, so labels and check boxes that only get a text color follow the parent's new background (`inherited` in the log). Controls that would fall back to a different default when reset (e.g. TextBox) keep their color.
- Applications tried: Git Extensions (.NET 10, startup hook), Greenshot (.NET Framework 4.8, WPF loop), Greenshot 1.3 (.NET Framework 4.7.2, WinForms loop, harness as code), mRemoteNG (.NET 10, DockPanelSuite), the Krypton Toolkit samples (.NET 9: grid, input form, ribbon, docking), the ScottPlot WinForms demo (.NET 10, 45 demos, with `FORMWIND_WALK`), an MDI shell (.NET 8: merged menus, window list, maximized child, children opened later), KeePass 2.61.1 (.NET Framework 4.8, harness as code), Open Live Writer (.NET Framework 4.7.2, harness as code, several UI threads; without the C++ Ribbon only the splash screen and the wizard), ShareX 19.0.2 (.NET 9, its own dark theme, 8 forms with `FORMWIND_OPEN`; later versions use Avalonia for the UI), the AdvancedDataGridView sample (.NET 8, a DataGridView with filter and search toolbars), a business form (.NET 8: master-detail grids, `BindingNavigator`, `ErrorProvider`, cell and row errors), Bulk Crap Uninstaller 6.3 (.NET 8, ObjectListView; its administrator manifest was run without elevation with `__COMPAT_LAYER=RunAsInvoker`, only the list and the first-start wizard), a DPI/RTL trial form (.NET 9; `-Dpi`, `SetColorMode`, `RightToLeft`/`RightToLeftLayout`). The problems found became the behaviors and tests described in this README. In applications with their own theme engine (mRemoteNG) dark mode shows only partially, because colors assigned by the application are kept; this is expected.

### Visual regression tests

`tests/FormWind.Tests/Visual` renders the showcase forms (`MainForm`, `ControlsForm`) and the calendar in light and dark mode and compares them with the PNGs in `tests/FormWind.Tests/Baselines` (net472 only). A pixel counts as different when the sum of its RGB differences exceeds 48; if more than 0.2% of pixels differ the test fails and `*.actual.png` plus a red-marked `*.diff.png` are written to `VisualDiffs` in the output folder.

Because font rendering depends on the environment, baselines are kept per environment: `Baselines/win26200-96dpi-cleartype/` (Windows build, DPI, font smoothing). If there is no folder for the current environment, the tests are skipped. After intentional visual changes, regenerate the baselines and commit them after reviewing the PNG differences:

```powershell
$env:FORMWIND_UPDATE_BASELINES = "1"; dotnet test tests/FormWind.Tests -f net472 --filter Category=Visual; $env:FORMWIND_UPDATE_BASELINES = ""
```

**CI.** `.github/workflows/ci.yml` builds, tests on four targets (net472, .NET 8, 9, 10), checks coverage and packs on every push and PR. Because `FORMWIND_VISUAL_EXPORT` is set, the visual tests write their renderings to the `visual-snapshots` artifact; if there is no baseline for the CI environment yet, the tests only export and the workflow prints a warning. CI is pinned to the `windows-2022` image; if the image changes, the baseline folder name changes too. Adding the `win…` folder from the artifact under `tests/FormWind.Tests/Baselines/` and committing it enables comparison for that environment.

### Website and documentation site

`website/` holds the landing page and the documentation (getting started, presets, classes, layout, controls, DataGridView, charts, Krypton, Designer, DPI, accessibility, tools, performance, API stability). It is static HTML with no build tools; `website/build.ps1` wraps the pages in the shared layout and writes them, with the screenshots from `docs/`, to `artifacts/website`. With `-Reference` it also generates the class reference below into `reference/`. The site is in English (at the root) and Turkish (`tr/`): every page has a counterpart with the same name under `website/en/` and `website/tr/`, the header button links to the other language's page, and shared texts are in `website/strings.json`. When a behavior described in this README changes, update the matching page in both languages too.

```powershell
.\website\build.ps1 -Reference
```

`tools/FormWind.Docs` renders every example off-screen with FormWind itself (PNG; light and dark for components and controls) and generates a class reference with previews; the color palette (v3 and v4) and icon names are included too:

```bash
dotnet run --project tools/FormWind.Docs -c Release -- artifacts/docs-site
```

The examples are defined in `tools/FormWind.Docs/Catalog.cs`. `.github/workflows/docs.yml` builds the website with the class reference on every push and uploads it as the `website` artifact; to publish to GitHub Pages, set the Pages source to "GitHub Actions" in the repository settings and define the `DEPLOY_DOCS=true` repository variable.

### Conversion tool

Converts hard-coded style assignments in existing forms to classes:

```bash
dotnet run --project tools/FormWind.Convert -- src/MyApp --report conversion.md
```

```bash
dotnet run --project tools/FormWind.Convert -- src/MyApp --write
```

| Designer line | Class |
| --- | --- |
| `BackColor` / `ForeColor` | `bg-*` / `text-*`: the palette color, the closest palette color within 10 units (with a note), otherwise `bg-[#hex]` |
| `Font` | `text-sm` … `text-4xl` or `text-[13px]`; a `Semibold`/`Bold` family or `FontStyle.Bold` → `font-semibold`/`font-bold`; `italic`, `underline` |
| `Padding` | `p-*`, `px-*`/`py-*` or per side (4 px = 1 unit; `[7px]`) |
| `FlatAppearance` | `BorderSize` → `border-0`/`border`, `BorderColor` → `border-*`, `MouseOverBackColor` → `hover:bg-*`, `MouseDownBackColor` → `active:bg-*` |

- The default is a dry run: it prints what would change and the notes. `--write` updates the files in place; commit first. The files' BOM is preserved.
- Converted lines are removed and a `SetClasses` call is written instead; if the control already has a `SetClasses` call, the classes are appended to it. A `TailwindProvider` is added if the file has none, and it is added to the `components` container so it is disposed with the form. If the class has no `components` field, one is added; if `Dispose` does not call it, a note is added to the report.
- Code from Visual Studio's new Designer, which does not write `this.` (`buttonOk.BackColor = ...`), and controls generated as local variables (`GenerateMember = false`) are supported; the control is passed to `SetClasses` the way it is written in the source.
- Only **Control**s created in `InitializeComponent` are converted. ToolStrip items, columns and local cell styles (`dataGridViewCellStyle1`) cannot take `SetClasses`; their lines stay. The application's own types are resolved to a WinForms type by following the `class X : Y` declarations in the target folder and its parent folder (sibling projects) (e.g. a class deriving from `ToolStripComboBox` is not converted); types that cannot be resolved are converted with a note.
- The old Designer format (`this.` prefix, brace on the same line as the method, tab indentation) is supported too.
- **Visual Basic:** `.Designer.vb` files are converted too (a folder is searched for both; the language is detected from the file's content). The output uses VB syntax: `Me.TailwindProvider1.SetClasses(btnSave, "...")`, the field is `Friend WithEvents TailwindProvider1 As FormWind.TailwindProvider`; without fields it is added before `End Class`. Font sizes like `10.5!`, color components like `CType(CType(37, Byte), Integer)` and `CByte(37)`, and lines with or without the `Me.` prefix (new Designer) are recognized. The application's own types are resolved from `Class X` / `Inherits Y` declarations. On a form created with `dotnet new winforms -lang VB` and styled by hand, the converted project built without warnings or errors, and colors and fonts were identical in a `-Dump` comparison.
- System colors (`SystemColors.*`), the application's own color expressions (`AppColor.X.GetThemeColor()`), derived fonts (`new Font(Font, FontStyle.Bold)`) and font families other than the theme's are not converted; the line stays and is noted in the report.
- **Verified on a real project (Greenshot 1.3, .NET Framework 4.7.2):** the converted project built with Visual Studio's MSBuild without warnings or errors; colors and fonts were identical on 311 controls in 7 forms, padding differed by 1 px (DPI rounding).
- **Verified on a real project:** 360 classes were generated in 74 Designer files of Git Extensions; the result built without warnings or errors (in a project that treats warnings as errors and uses StyleCop). In values taken with the harness's `-Dump` mode before and after the conversion, colors and fonts of 270 controls were identical; the padding differences came from DPI behavior and revealed two library bugs (double scaling; see [DPI](#dpi)).
- `--config tw.config.json` uses the application's own palette; `--tolerance` changes the closest-color threshold.
- The report marks controls that only got notes (nothing converted) with `(nothing converted)`. When nothing is left to change, a second run prints `Değiştirilecek bir şey yok.` ("nothing to change") and leaves the files alone; BOM and line endings are kept. Tried on KeePass 2.61.1: the `-Dump` values before and after the conversion were identical.
- **Pre-migration audit (`--audit`):** `formwind-convert src/MyApp --audit --report audit.md` lists, without touching the files, the places that may need a manual look after FormWind is added, with file:line and a suggestion; code outside the Designer files is scanned too (Roslyn syntax tree, no build needed):
  - `BackColor`/`ForeColor` assigned in code (a system color, a fixed color or the application's own value; `ResetBackColor()` suggested for going back to the default),
  - `ListViewItem`, `ListViewSubItem` and `TreeNode` colors; DataGridView cell styles assigned in code,
  - controls that draw themselves (`OwnerDraw`, `DrawMode = OwnerDraw*`, `DrawItem`/`DrawNode`/`CellPainting` subscriptions, `OnDrawItem` overrides),
  - fixed-size buttons with `AutoSize` off and an image beside the text (set in the Designer or in code through a helper),
  - `SetWindowTheme` calls and calls of methods that wrap it; loading RTF (`Rtf`, `SelectedRtf`, `LoadFile`).

  On the original KeePass 2.61.1 source it found 82 places, among them everything the manual integration found from screenshots: the group tree color, the entry list item colors, the entry details loaded as RTF, the repeat-password box's error color and the "Tools" button. Types are resolved from field and local declarations; receivers without a found declaration fall under "colors assigned in code". Only C# is scanned.

Screenshot of the showcase form (`--controls` opens the controls form):

```bash
samples/FormWind.Showcase/bin/Debug/net472/FormWind.Showcase.exe --snapshot light.png
```

## API stability

FormWind is in preview (0.x): minor versions may contain breaking changes; they are listed under *Changed* or *Removed* in the [changelog](CHANGELOG.md). Three contracts are kept apart:

- **Public API:** `TailwindProvider`, the controls, `TwTheme`, presets, events and the classes. Every public member is tracked (see [Public API](#public-api)); from 1.0 changes follow [Semantic Versioning](CONTRIBUTING.md#versioning).
- **Class syntax:** what a class means is pinned by tests that do not compile against it: `tests/FormWind.Tests/Golden/parse.txt` holds the resolved style of about 70 sample class strings (with their variants, parts, presets and the v4 palette), and `GoldenParseTests` fails when one changes. An intended change is written with `FORMWIND_UPDATE_BASELINES=1` and listed in the changelog.
- **Theme file:** `"version"` in `tw.config.json` is the file format version (`TwTheme.FileVersion`, currently 1; a file without it is version 1). A file written for a newer format is rejected with `NotSupportedException` instead of being read wrongly.
- **Invalid theme file:** a syntax error, an unexpected value type (a number instead of a component) or an invalid color is rejected with `FormatException("tw.config.json: …")`; the original error is the `InnerException`. A newer file version is a `NotSupportedException`.

**Experimental extension points (FW9001).** `IStyleApplier`, `TwApplyContext`, `TwAppliers`, `WinFormsApplier`, `TwStyle`, `TwParsed`, `TwOriginal`, `TwTheme.Parse` and the helpers that take a `TwStyle` (`TwFonts.Create`, `TwChrome.FromStyle`, `TwGradient.FromStyle`, `TwLayout.Apply`, `TwToolTip.ResolveStyle`) are tied to System.Drawing and WinForms types and may change when the platform-neutral core is extracted. Using them is a compile error (FW9001) until it is suppressed:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);FW9001</NoWarn>
</PropertyGroup>
```

## Packaging

```bash
dotnet pack FormWind.sln -c Release
```

Packages are written to `artifacts/packages` (`FormWind`, `FormWind.Krypton` and `.snupkg` symbol packages). The `FormWind` package includes the FW001 analyzer under `analyzers/dotnet/cs`. Packages include SourceLink information, so the debugger can step into FormWind's source from GitHub. The assemblies are strong-named (`PublicKeyToken=1cea47bdc82de2bf`, key `FormWind.snk` in the repository), so strong-named .NET Framework applications can reference them. The NuGet pages use the short READMEs in `docs/nuget/`. For local use, add this folder to Package Sources in Visual Studio. Pushing a `v*` tag runs `.github/workflows/release.yml`, which publishes the packages to nuget.org and creates the GitHub Release (see [CONTRIBUTING](CONTRIBUTING.md#releasing)).

## Known limitations and roadmap

- [x] Colors, fonts and spacing applied by the preview are not written to `Designer.cs` (properties managed by the classes and the preview's side effects are not serialized; tested with CodeDOM serialization and a save/reopen loop)
- [x] Quick component selection via smart tag (standard controls included)
- [ ] Self-drawing controls ignore cell styles and may stay light in dark mode (owner-drawn grids: `TwGrid.EnableDarkInversion`)
- [x] Charts: `TwChart` (line, area, bar, stacked), `TwDonutChart`, `TwSparkline`, `TwStat`
- [x] Grid: search, range filters, grouping, totals row, row details, saved layout, CSV export, density variants; sparkline, date, number, link, tags and actions columns
- [x] `row-span`, `grid-rows` for grid; list rows with varying heights (`ItemHeights`); a DataGridView VirtualMode helper (`TwVirtualRows<T>`)
- [x] .NET 8+ Designer: class builder in the smart tag, component changes that keep the utilities, the project's theme in the Designer
- [ ] Porting the `ClassesEditor` window to the .NET 8+ out-of-process Designer with the Designer SDK
- [x] Presets (Tailwind, Fluent, Ant Design, Material), semantic colors and `AutoStyle`
- [x] Krypton: CheckBox, RadioButton, CheckButton, input controls, ComboBox, GroupBox, HeaderGroup, ListBox, KryptonForm; a palette that follows the theme (`TwKryptonTheme`), version matrix, sample and the `--Krypton` template option
- [x] License: MIT

## License

FormWind is distributed under the [MIT License](LICENSE). Third-party material (the Tailwind CSS color palette, the test font): [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
