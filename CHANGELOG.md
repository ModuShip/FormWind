# Changelog

All notable changes to FormWind are documented in this file. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](CONTRIBUTING.md#versioning).

## [Unreleased]

### Added

- `TwDpi` is public: `TwDpi.Simulate(dpi)` makes FormWind draw as on a display with that DPI until the returned scope is
  disposed (class sizes and `text-*` fonts get the pixel sizes they would have there), so a machine at 100 % can check 125 %
  and 150 % together with `TwOffscreen.Render`. `TwDpi.DeviceDpi(control)`, `SystemDpi`, `FontScale(control)` and
  `SimulatedDpi` return the values FormWind scales with. Before, the only way was reflection on internal members.
- `TwHeading`: a two-line title (`Text`, muted `Subtitle` styled with `hint:`) whose lines end with an ellipsis; as a
  `TwToolbar` item with `TwToolbarOverflow.Shrink` it is a window title with a record count under it.
- `TwPageHeader`: a page title, a muted subtitle and the controls added to it on the right; when the window is too narrow the
  controls move under the title and wrap (`IsStacked`). New `page-header` component (`label:` title, `hint:` subtitle, `gap-*`),
  applied by `AutoStyle`.
- `TwCard`: the control form of the `card` component, with a `Title`, a muted `Description`, header controls
  (`Header.Controls`) and a body made of its other children; the header hides itself while empty. `AutoStyle` gives it `card`.
- `TwFormHost`: shows an existing `Form` as a page (`TopLevel = false`, borderless, filling the host). A form designed with
  `AutoScaleMode.None` is scaled to the DPI once (`ScaleDesignSize`), its title is offered as `Title` / `TitleChanged` and copied
  to `TitleTarget`, Enter / Esc press its `AcceptButton` / `CancelButton`, and `FormClosed` is raised when it closes itself.
- `TwPopover.ShowOnHover(anchor, delay)`, `StopHover`, `HoverCloseDelay` and `IsOpenFromHover`: a popover that opens when the
  pointer rests on an anchor, does not take the focus, and stays open while the pointer is over the anchor or the popover.
- `TwPanel.ScrollToFocused`: `false` keeps an `AutoScroll` page from jumping to a child focused with the mouse (a list or grid
  that is clicked); focus moved with the keyboard still scrolls.
- `TwPeriodPicker.DateStyle` (`TwPeriodDateStyle.Abbreviated`: "1 – 31 Eki 2026"), `DateFormat` and `FormatRange`; a
  `TwFilterKind.Period` chip follows a format change without raising `FiltersChanged`.
- Built-in icons `bookmark`, `bookmark-filled` and `factory`, drawn as SVG (the icon fonts have no glyph for them). A glyph
  registered with the same name wins. The FW003 analyzer knows them.
- `TwTabStrip.Overflow` (`TwTabOverflow`): `Scroll` makes a strip whose tabs do not fit scroll instead of moving tabs to the
  "more" menu. It has ‹ › buttons at the edges (they scroll, they do not change the selection), edges that fade where more
  tabs follow, and Shift+wheel or a tilt wheel (a plain wheel still scrolls the page). Tabs keep their order, and the selected
  tab is scrolled into view, animated. `ScrollAndMenu` adds a "more" button that lists every tab, with the selected one
  checked. `Menu` (the default) and `Clip` are the old behaviors. New members: `IsScrollable`, `ScrollOffset`,
  `MaxScrollOffset`, `ScrollTabIntoView`, `ScrollBackButtonBounds` / `ScrollForwardButtonBounds`, `GetVisibleTabBounds` and
  `MinimumScrollWidth`. Tabs scrolled out of view are offscreen for screen readers.
- `TwToolbar` narrows an `AutoSize` tab strip that scrolls (after the `Shrink` items), down to its `MinimumScrollWidth`, before
  it moves the strip to the "more" menu, and widens it again when there is room.
- A label whose text `AutoEllipsis` cuts short (`truncate`, the caption of a `TwSettingRow`) shows its full text in a themed
  `TwToolTip` that follows dark mode. It replaces WinForms' own light tooltip, and appears only while the text is actually cut.

### Fixed

- In dark mode a `Pills` or `Segmented` tab strip with the default `tabs` classes drew the active tab as white text on a white
  pill / segment, and the segmented track stayed light gray. Without `selected:bg-*` / `fill-*` classes they now take dark
  surface shades (`surface-700` for the active tab, `surface-800` for the track); classes given to the strip still win, and
  light mode is unchanged.

### Changed

- `TwTabStrip.OverflowMenu` is hidden from the designer and maps onto `Overflow`: `false` turns `Menu` into `Clip` and
  `ScrollAndMenu` into `Scroll`. Existing code keeps working.

- **Breaking:** `TwSettingRow.EditorWidth` is 0 by default (the editor's own width), as the 0.1.6 notes described the
  behavior. Before, an editor that was not `AutoSize` was forced to 160 px unless 0 was given; set `EditorWidth = 160` to keep
  the old layout. Designer files that relied on the old default do not contain the property and change too.
- Documentation: migrating away from an application's own DPI factor (with an example), testing other scales with
  `TwDpi.Simulate`, a responsive stat grid (`grid grid-cols-2 lg:grid-cols-4`), card surfaces (`shadow-*`, `bg-white/70
  backdrop-blur-*`), and that `SortChanged` is an event of `TwDataList`, not of its header.
- A regression test covers registering fonts and loading the theme from static fields before `Main` calls
  `SetCompatibleTextRenderingDefault` (.NET Framework); it passes, so the startup error reported for earlier versions does not
  occur.

## [0.1.6-preview.1] - 2026-11-05

### Added

- `TwMotionOptions.Completed` runs code once, on the UI thread, when a transition or window motion has fully finished
  (`CrossFade`, `SlideIn`, `SlideOut`, `Resize`, `TwWindowMotion.SlideIn` and `FadeOut`): move a button to the toolbar after
  the sidebar collapsed. When nothing is animated (animations off, zero duration, nothing to take a picture of, a panel that
  is already in the asked state) it is called synchronously after the change is applied. There are no `Task`-returning
  overloads; complete a `TaskCompletionSource` from `Completed` to `await` a motion.
- `TwMotion.Timings.Scale` multiplies the duration and delay of every animation (default 1), like Android's animator
  duration scale: set 5 to watch transitions in slow motion, 0 to make every animation immediate.
- `TwTheme.Explain(classes)` describes what each class resolves to (`BackColor = #2563EB`, `[Hover] BackColor = #1D4ED8`),
  with unknown classes flagged and their suggestion, so `Parse(...).Resolve(...)` is no longer needed to see a value.
- `TwUnknownClassesEventArgs.Suggestions` holds the "did you mean" of each unknown class, and `ToString()` lists them.
- FW004 analyzer: warns about malformed arbitrary values (a missing or wrong unit such as `duration-[120]`, unbalanced
  brackets, a color that is not hex) in class strings given to `TailwindProvider.SetClasses`, `SetColumnClasses`,
  `SetRowClasses` and `Classes` properties.
- `TwListItem` row parts that remove the last owner-drawn lists: a clickable toggle icon (`ToggleIconName`, `ToggledIconName`,
  `IsToggled`, `ToggleColor`, `ToggleText`), an inline action button (`ActionText`, `ActionClasses`) and an inline progress bar
  with text (`ProgressValue`, `ProgressText`, `ProgressColor`). `TwListBox` raises `ItemToggle` and `ItemActionClick`
  (`TwListItemEventArgs`), flips and presses them from the keyboard (Space, Ctrl+Enter) and from code (`ToggleItem`,
  `PerformItemAction`), exposes the click areas (`GetToggleBounds`, `GetActionBounds`), shows hover and pressed looks, mirrors
  in right-to-left layouts and offers the parts to screen readers as child objects of the row (`TwTexts.Toggle` names an
  unnamed toggle).
- `TwTwoLineColumn` and `TwTwoLine`: a table cell with a bold title, a muted second line and an optional status dot, with an
  optional monospace title (`MonospaceTitle`) for record codes. The value is a `TwTwoLine`, or any value whose second line and dot
  come from the row's data object (`SubtitleMember`, `DotColorMember`, `ValueSelector`). The cell reports the height of two
  lines as its preferred height and `FitRowHeight()` sets the grid's rows to it; it follows selection colors, dark mode, DPI
  and right-to-left layouts, and shares its drawing with `TwListItem` rows.
- `TwGridTreeNode.Subtitle` and `TwGridTree.SubtitleSelector` give the tree column a bold name with a muted second line ("Line ·
  2 sub-groups"); rows with a second line get the height two lines need. `TwGridTree.GetSubtitle(node)` returns it.
- `TwBadgeColumn.Selector` (`Func<object?, TwBadge>`) chooses a badge's text and classes from the cell's value, so a "+12 %" chip
  colored by sign or a "new" chip no longer needs a hand-drawn cell. It wins over `Variants`; `TwBadge` is a small struct
  (`Text`, `Classes`) rather than a tuple, which is friendlier to .NET Framework callers and to the public API files. A `null`
  text keeps the cell's text and an empty text draws no badge.
- `TwDataList.Columns` (`TwDataListColumn`: `Text`, `Width`, `Weight`, `Alignment`, `Sortable`, `Key`), `ColumnGap`,
  `TwDataListDrawEventArgs.GetColumnBounds(column)` and `TwDataList.GetColumnBounds(column, index)` line up the parts of a row
  like a table. `TwDataList.CreateHeader()` returns a `TwDataListHeader` with the column names; a click, Enter or Space on a
  sortable name toggles `SortColumn` / `SortOrder` (▴ / ▾) and raises `SortChanged` (the list does not sort the data). The
  header is a tab stop (←/→, Home/End move between sortable columns) and its columns are accessible column headers that name
  their sort state. `TwTexts.SortBy`, `SortedAscending` and `SortedDescending` hold the screen reader texts.
- `TwFilterKind.Period`: a `TwFilterBar` filter whose value comes from a `TwPeriodPicker` panel opened under the chip
  ("Tarih: Son 90 gün"). `TwFilter.Period` is the picker (presets, shifts), `TwFilterBar.GetPeriod(key)` returns the days and
  `SetPeriod(key, start, end)` sets a custom range.
- `TwOffscreen.Render(form)` pictures a whole window for visual regression tests: a form that is not shown yet is opened
  off-screen, laid out and painted (motion off), and the content of the form and of every window it owns (`TwSheet`,
  `TwPopover`, dialogs) is composed with `Opacity`, `TransparencyKey` and `Region` applied. `Control.DrawToBitmap` misses
  the owned windows and the transparency. `TwOffscreenOptions` sets `IncludeOwned`, `ExpandToOwned`, `Background`,
  `DisableMotion` and `CloseWhenDone`.
- `TwCaptionButtons.ButtonStyle = TwCaptionButtonStyle.Dots` draws the minimize, maximize and close buttons as round yellow,
  green and red dots whose glyphs appear while the mouse is over them (`CloseDotColor`, `MinimizeDotColor`,
  `MaximizeDotColor`).
- `TwHeatmap.HighlightTextColor` sets the header text color of `HighlightColumns` (a red Sunday); `TwHeatmap.SelectionColor`
  sets the outline of the selected cell.
- `TwDialogKind.None` shows a dialog without an icon; `TwDialog.Prompt(owner, options)` follows `options.Kind`.
- Built-in icons `log-out` (`sign-out`), `sidebar`, `panel-right`, `wrench` (`tool`), `gauge` and `paperclip` (same glyph as
  `attach`). The FW003 analyzer knows them.

### Changed

- Documentation: `TwMotionOptions.IncludeMargin` is for panels whose gap comes from `Margin` (FlowLayout, TableLayout, manual
  layout); Dock layout ignores `Margin`. The README explains the wait before a `TwTransition.CrossFade` starts (the page's
  paint time) and `TwMotion.Prewarm`, that `text-[..px]` sizes are 96 DPI pixels (an application's own scale factor scales them
  twice), and that the FW003 analyzer only sees icon names given as literals (use `StyleError` / `TwIcons.IsKnown` for the rest).

### Fixed

- `TwTransition.Resize(panel, change)` works for a sidebar that `change` hides or shows: a panel that is hidden before or
  after the change is treated as a zero-thickness strip at its docked edge, so opening a hidden panel animates (it
  returned without animation) and closing one reveals its picture instead of only cross-fading. A width of 0 animates
  too.
- While a panel grows under `TwTransition.Resize`, the part of its place it has not covered yet fades its old content out
  instead of turning into the parent's background color at once (a white block that the sidebar then grew into). With
  `FadeRest = false` the background color stays, so moved text is still never seen twice.
- `Resize` covers the form's client area, not only the panel's parent: controls that `change` moves out of the parent (a
  row moved to the form) no longer show their new layout for a frame. The panel's subtree gets its window handles and is
  repainted before the "after" picture is taken, so newly shown or reparented children are in it.
- The picture that covers a transition (`TwFadeOut.Cover`) waits for the desktop window manager before it returns, so the
  change underneath cannot reach the screen first (a one-frame flash).
- `TwSettingRow.EditorWidth = 0` keeps the editor's own width (a `TwSelect` of 180 px stays 180) instead of the 160 px
  the row had laid it out with. The row remembers the width the application gave the editor.
- `TwToolbar` no longer subtracts its padding twice when it computes the room for its items (`DisplayRectangle` already
  excludes it), so a `Shrink` item is not shortened (an ellipsized title) while everything fits. A `Shrink` item whose
  `AutoSize` is off is measured by its content, so it grows back when there is room.
- `TwToolbar`'s overflow menu never shows an empty row: the text falls back to `AccessibleName`, then to the selected tab
  of a `TwTabStrip` inside the item, then to `TwTexts.More`. A `TwTabStrip` wrapped in a container becomes a submenu too.
- `TwChip` asks its parent layout for its preferred size again when it gets a parent, so an `h-*` class set before the chip
  is added applies.

### Behavior changes

- `TwToolbar` places its items at its padding, not at twice the padding, and gives them the full inner height; the
  preferred size no longer counts the padding twice.
- `TwTheme.SuggestClass` (and so the Debug output and `UnknownClasses` suggestions) fixes an arbitrary value with a missing
  or wrong unit: `duration-[120]` suggests `duration-[120ms]`, `w-[120]` suggests `w-[120px]`.
- `TwMotion.Timings.Reset()` also sets `Scale` back to 1.
- The documentation of `UnknownClasses` and `StyleError` now says which reports what: a class FormWind does not recognize
  (including `duration-[120]`) goes to `UnknownClasses`; `StyleError` is for errors while applying styles.
- The outline of the selected `TwHeatmap` cell is drawn in the control's text color by default instead of `accent-*`,
  which blended into a scale in the accent color. Set `SelectionColor` to bring a colored outline back.
- `TwDialog` takes the surface of the theme's `card` component when `TwDialog.LightBackColor` / `DarkBackColor` and
  `TwDialogOptions.BackColor` are not set, so dark dialogs follow the theme's palette instead of the bluish `gray-800`.
  Set `TwDialog.DarkBackColor` to keep the old color.

## [0.1.5-preview.1] - 2026-11-04

### Added

- `TwMotionOptions` gives one transition or window motion its own duration, curve (`Easing` or a `TwCubicBezier`
  `Curve`), slide distance and panel settings. `TwTransition.CrossFade`, `SlideIn`, `SlideOut`, `TwWindowMotion.SlideIn`
  and `FadeOut` take it, and `TwWindowMotion.Attach(window, edge, enter, exit)` gives the entrance and the exit their own
  (for example a spring entrance and an ease-in exit).
- `TwWindowMotion.Distance` sets how far windows slide (default 24 px); `TwMotionOptions.Distance` sets it for one call.
- `TwMotion.Timings.CrossFade`, `Slide`, `WindowIn` and `WindowOut` set the default durations of page cross-fades, panel
  slides and resizes, and window entrances and exits.
- `TwMotionOptions.IncludeMargin` slides the panel's `Margin` with it, so the gap beside an inspector comes and goes with
  the panel. `TwMotionOptions.FadeRest = false` puts the rest of the parent in its new layout at once instead of
  cross-fading it, so text that moves (grid columns) is never seen twice.
- `TwTransition.Resize(panel, size)` animates the width (or height) of a docked panel, such as a sidebar collapsing to
  icons, with one layout: the panel's picture opens or closes from its docked edge while the rest cross-fades.
  `Resize(panel, change)` applies a change that sets the size together with other changes (labels hidden in a collapsed
  sidebar) under the picture of the old look.
- `TwMotion.Prewarm(control)` creates the window handles of a control and all its children and lays them out while it is
  hidden, so a page built in the background opens about twice as fast the first time.
- `TwMotion.SetDoubleBuffered(control, value)` turns double buffering on for controls that do not expose it, such as a
  `DataGridView` without classes or a `ListView`.
- Packages carry their release notes: the section of this file for the version is the package's release notes.
- `TwHeatmap.SelectedIndex` (with `SelectedIndexChanged`; with `SelectOnClick` a click or Enter selects the cell), `HighlightColumns` (a faint band
  behind columns such as today), `CellSize` (fixed cells; `GetPreferredSize` shows them all) and `GetCellBounds(row, column)`
  for placing a popover next to a cell without reflection.
- `TwGridTree.SetTotal(column, nodes => …)` computes a parent row's value from its children (a ratio that does not add up),
  and `TwGridTree.GetValue(node, column)` returns a node's own or computed value.
- `TwStat.ValueScale` (size of the value relative to the font, default 2), `TwStat.Unit` (a small unit on the value's
  baseline, read with the value by screen readers) and `TwStat.ShowTrend`.
- `TwDialog.DarkBackColor`, `TwDialog.LightBackColor` and `TwDialogOptions.BackColor` give dialogs the application's own
  surface instead of `gray-800`; `TwDialogOptions.OkText` names the positive button ("Save");
  `TwDialog.Prompt(owner, options)` asks for text with every option (`OkText`, `InputMaxLength`, `InputLabel`).
- `TwIcons.IsKnown(name)`. An unknown icon name on a styled `TwIcon`, `TwButton` or `TwChip` is reported through
  `TailwindProvider.StyleError`, and the new FW003 analyzer warns about an unknown literal assigned to `IconName`.
- `TwTextColumn` and `TwSelectColumn`: editable grid columns for data-entry tables. Cells are drawn as input boxes with a
  placeholder, a red border when `Validate` reports an error and a focus ring; only the focused cell hosts a real editor
  (`EditOnFocus` opens it when the cell is reached), and a select cell opens its list on click, Space, F4 or Alt+↓.
- `TwSheet` opens a form as a frameless sheet inside its owner with the owner dimmed (`TwSheetOptions.FullScreen` fills
  it); `TwSheet.Attach(owner)` turns the owner's existing `ShowDialog(owner)` calls into sheets without code changes.
- `TwWindowChrome` removes a form's title bar while keeping dragging from empty surfaces, double-click to maximize, Aero
  Snap and resizing; `TwCaptionButtons` draws minimize / maximize / close.
- `TwFilterBar`: applied filters as chips with an "Add filter" menu; select, text and fixed filters; `LoadValues` sets
  values without raising `FiltersChanged`.
- `TwCommandPalette`: a Ctrl+K palette with grouped, accent-insensitive filtering, ↑/↓/Enter/Esc and "Search … in" rows
  (`AddSearch`, `QueryCommands`).
- `TwPeriodPicker`: presets, a custom range picked on a calendar and an optional shift segment, in a popover.
- `TwCardStrip`: a paged horizontal strip of cards with ‹ › buttons, a "1 / 3" indicator and a minimum card width.
- `TwChartKind.StackedBar100`: horizontal bars split into each row's shares of 100%.
- `TwHeatmap.CreateLegend()` returns the legend as its own control (`TwHeatmapLegend`).
- `TwListBox.BeginUpdate()` / `EndUpdate()` defer re-measuring and repainting while many items change.
- `TwCapture.Take(control)` and `TwFadeOut` are public: a picture of a control, and a fading cover over a change.
- The `chip` component has `transition-colors duration-200`, so checking a filter chip fades its colors.
- `TwTexts` has texts for the new components (`Previous`, `Next`, `AddFilter`, `CommandPlaceholder`, `NoResults`,
  `SearchIn`, `Today`, `Yesterday`, `LastDays`, `ThisMonth`, `LastMonth`, `ThisYear`, `CustomRange`, `AllShifts`, `Apply`).

### Fixed

- `TwSettingRow` no longer squeezes an `AutoSize` editor below its preferred width (a tab strip moved its last tab to the
  overflow menu; an auto-sized panel of a status text and a button was cut), and follows it when it grows.
- `TwSettingRow` keeps the font and color that the application gives `CaptionLabel`, `DescriptionLabel` or `UnitLabel`
  (classes or properties) when the theme changes; they used to be replaced by the row's.
- Setting `TwSettingRow.EditorWidth` to its current value no longer lays the row out again, so setting it from the
  editor's `Layout` or `SizeChanged` no longer loops.
- `TwGrid.SetTree` builds its rows from `RowTemplate`, so they keep the row height of the classes (`row:h-*`, `table`)
  instead of the 22 px default.
- `TwToolbar`: a `Shrink` item no longer collapses to a letter while other items could move to the "more" menu: it
  shrinks to a readable width (96 px or its `MinimumSize`) first, then items move to the menu, and only then it gets
  narrower. Its width is measured again when its text or font changes (classes applied after the first layout).
- `TwChip` with an `h-*` class reports that height as its preferred size, so a `FlowLayoutPanel` no longer shrinks it.
- `TwSettingRow` inside a container with its own horizontal padding (a `card p-4`) no longer indents its text a second
  time; `h-*` is the row's minimum height with the content centered; an editor without its own font gets the container's
  font instead of the row's `text-sm`, so it matches the other inputs of the form.
- .NET Framework: FormWind initializes WinForms' DPI scaling itself before reading the DPI, so `text-[px]` and spacing are
  right on the first form of a per-monitor DPI aware application.

### Behavior changes

- `TwWindowMotion.Attach` slides the window in every time it is shown, also when it is shown again after `Hide()` or after
  `ShowDialog` returned (it used to slide only the first time). A hidden attached window is kept transparent until it
  slides in again.
- `TwWindowMotion.SlideIn` on a window that is not on screen yet (created but not shown, or being shown in `Load` or
  `VisibleChanged`) makes it transparent and slides it in once it is shown, instead of doing nothing.
- The default durations of `TwTransition` and `TwWindowMotion` come from `TwMotion.Timings` (same values as before:
  250 ms, window exit 180 ms).
- Flex and grid layout move a container's children together in one `DeferWindowPos` batch instead of one `SetBounds`
  per child, so relayout of a page with many controls is faster (a busy page in the samples: about 47 ms to 38 ms). The
  children get the same bounds as before, but their `LocationChanged`, `SizeChanged` and own layout now run after the
  container has computed all of its children, not between them. Controls that constrain their own size (`TextBox`,
  `ComboBox`, an `AutoSize` `Label` stretched wider than its text, controls with `MinimumSize` or `MaximumSize`) and
  controls without a handle are still placed with `SetBounds`.

## [0.1.4-preview.1] - 2026-10-26

### Added

- `TwTransition.SlideIn(panel, edge)` and `SlideOut(panel, edge)` show or hide a side panel sliding from its edge. The
  layout is done once: a picture of the panel slides into its place while the rest of the parent cross-fades to its new
  layout, so a grid beside the panel is not relaid out and torn every frame.
- `TwWindowMotion`: `Attach(window, edge)` makes a window (a sheet, a side window) slide in from an edge each time it is
  shown and its last picture fade out toward that edge each time it closes; `SlideIn` and `FadeOut` do it once. The
  window closes at once. `TwEdge` names the edge.
- `TwMotion.AfterPaintAsync(control)` completes once the control has painted what was pending, also while another
  control keeps repainting (where `Application.Idle` never comes).
- `TwMotion.Timings` sets the built-in durations no `duration-*` class reaches: chart opening, change and stagger, stat
  counting, popup opening and closing, attention pulse, dialog and toast fades. `Reset()` restores the defaults.
- `TwMotion.Preference` (`TwMotionPreference.System`, `On`, `Off`) gives the animation setting as one value an application
  can store with its own settings.
- `TwToast.Inset` keeps space free at the owner's edges, for example below a toolbar.
- `TwButton.RunBusyAsync(work)` runs the work with the button busy and makes it usable again when the work ends or fails.
- `TwDataList.IsLoading` (also `TwListBox`) fills the list with pulsing skeleton rows and ignores input while data loads.
- `TwStat.DefaultAnimateValue` turns value counting off (or on) for every stat that does not set `AnimateValue`.

### Behavior changes

These change how existing code behaves without changing the API, so they do not show in an API comparison.

- All animations run on one frame clock per UI thread that no longer depends on `WM_TIMER`. A control that keeps
  repainting (a chart opening, pulsing skeleton rows) used to stop every other animation, such as a page transition.
  Code that waited for `Application.Idle` or a `WM_TIMER` to know an animation had moved on should use
  `TwMotion.AfterPaintAsync`.
- `TwTransition.CrossFade` takes the picture of the old look from the screen after the pending paints are done (from the
  desktop window manager when the part is covered or off screen) instead of `DrawToBitmap`, which redrew the page (40–80
  ms), drew overlapping controls in the wrong order, doubled text in nested scrolled panels and drew a removed title bar
  back. The fade starts only after the new look is painted, so it is seen whole however long the change takes. Anything
  above the part on screen (a notification of the same window) is in the picture too.
- A `DataGridView` with classes is double buffered, so it is no longer painted row by row while data arrives.

### Changed

- A form's picture leaves out the invisible resize borders Windows 10 and 11 count in its bounds.
- The fade-out of closing dialogs and popovers no longer copies the wrong part of the screen in a DPI-unaware application
  on a scaled display (or a system-aware one on several monitors): the screen is read in physical pixels there and scaled
  to the window's size.
- Pictures that close or fade (dialogs, popovers, transitions) no longer play Windows' own window animation, which could
  leave them on screen for a moment after closing.

## [0.1.3-preview.1] - 2026-10-25

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
- `TwPopover` and the menus FormWind draws grow from 95% and fade in from the side they open from (140 ms) and fade out
  when they close (120 ms; the popover used to fade in and out in 150 ms).
- A focus ring that appears for keyboard focus closes in from 6 px outside onto the control (180 ms).
- `TwGrid.SetLoading(grid, loading, TwLoadingStyle)` and `TwGrid.SetLoadingStyle`: `TwLoadingStyle.Shimmer` sweeps a
  light band across the skeleton rows instead of the pulse.
- `TwTransition.CrossFade(host, change)` applies a change (a page switch, a theme change) under a picture of the old
  look that fades away (250 ms, or a given duration).
- `TwAttention.Pulse(control, count)` and `TwChip.Pulse(count)` send rings out from a control to draw attention to it.
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
- README: "Setup on .NET Framework" under DPI: the DPI-unaware, system-aware and PerMonitorV2 options, why
  PerMonitorV2 needs both the manifest and App.config, why App.config must not declare the section in `configSections`,
  and how to move an application with its own DPI factor (`Px()`-style helpers) to DPI-aware without scaling twice.

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

[Unreleased]: https://github.com/ModuShip/FormWind/compare/v0.1.6-preview.1...HEAD
[0.1.6-preview.1]: https://github.com/ModuShip/FormWind/compare/v0.1.5-preview.1...v0.1.6-preview.1
[0.1.5-preview.1]: https://github.com/ModuShip/FormWind/compare/v0.1.4-preview.1...v0.1.5-preview.1
[0.1.4-preview.1]: https://github.com/ModuShip/FormWind/compare/v0.1.3-preview.1...v0.1.4-preview.1
[0.1.3-preview.1]: https://github.com/ModuShip/FormWind/compare/v0.1.2-preview.1...v0.1.3-preview.1
[0.1.2-preview.1]: https://github.com/ModuShip/FormWind/compare/v0.1.1-preview.1...v0.1.2-preview.1
[0.1.1-preview.1]: https://github.com/ModuShip/FormWind/compare/v0.1.0-preview.1...v0.1.1-preview.1
[0.1.0-preview.1]: https://github.com/ModuShip/FormWind/releases/tag/v0.1.0-preview.1
