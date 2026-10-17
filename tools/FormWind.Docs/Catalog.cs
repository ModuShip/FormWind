using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind.Docs;

/// <summary>Sitedeki bir örnek: class'lar, onları uygulanacak kontrol ve isteğe bağlı açıklama.</summary>
/// <param name="Classes">Kontrole verilen class'lar (sitede kod olarak gösterilir).</param>
/// <param name="Make">Örnek kontrolü oluşturur.</param>
/// <param name="Note">Kısa açıklama.</param>
/// <param name="Themed">Açık ve koyu temada ayrı ayrı çizilsin mi (bileşenler ve kontroller).</param>
internal sealed record Example(string Classes, Func<Control> Make, string? Note = null, bool Themed = false);

internal sealed record Section(string Id, string Title, string Intro, IReadOnlyList<Example> Examples);

/// <summary>Belge sitesinin içeriği. Her örnek gerçek FormWind ile çizilir.</summary>
internal static class Catalog
{
    private static Func<Control> Text(string text, int width = 220, int height = 40) =>
        () => new Label { Text = text, AutoSize = false, Size = new Size(width, height), TextAlign = ContentAlignment.MiddleCenter };

    private static Func<Control> Button(string text, string? icon = null) =>
        () => new TwButton { Text = text, IconName = icon ?? "", Size = new Size(140, 38) };

    private static Func<Control> Card(int width = 220, int height = 90) =>
        () => new TwPanel { Size = new Size(width, height) };

    // Yerleşim örneklerinin kutuları; class'ları Tag'den uygulanır (bkz. Renderer).
    private static Func<Control> Boxes(int count, int width, int height) => () =>
    {
        var panel = new TwPanel { Size = new Size(width, height) };
        for (int i = 0; i < count; i++)
            panel.Controls.Add(new Label { Text = (i + 1).ToString(), AutoSize = false, Size = new Size(44, 32), TextAlign = ContentAlignment.MiddleCenter, Tag = "bg-indigo-500 text-white rounded-md" });
        return panel;
    };

    public static IReadOnlyList<Section> Sections { get; } = new[]
    {
        new Section("backgrounds", "Backgrounds and text", "bg-{color}-{shade} → BackColor, text-{color}-{shade} → ForeColor. Opacity with /0–100.", new[]
        {
            new Example("bg-blue-600 text-white", Text("Save")),
            new Example("bg-emerald-100 text-emerald-800", Text("Delivered")),
            new Example("bg-amber-50 text-amber-900 border border-amber-200", Text("Pending review")),
            new Example("bg-gray-900 text-gray-100", Text("Dark surface")),
            new Example("bg-gradient-to-r from-sky-500 to-indigo-500 text-white", Card(), "Gradient (TwPanel, TwButton, Panel)"),
        }),
        new Section("typography", "Typography", "text-xs … text-4xl (px × 0.75 = pt), weights resolve to installed families (Segoe UI Light, Semibold, Black).", new[]
        {
            new Example("text-xs", Text("The quick brown fox")),
            new Example("text-base", Text("The quick brown fox")),
            new Example("text-2xl font-semibold", Text("The quick brown fox", 320, 48)),
            new Example("text-lg font-light", Text("Light 300")),
            new Example("text-lg font-black", Text("Black 900")),
            new Example("italic underline text-blue-700", Text("Italic underline")),
        }),
        new Section("spacing", "Spacing", "p-*, px-*, py-* → Padding (4 px per unit). m-* is used by flex and grid layouts.", new[]
        {
            new Example("bg-sky-100 text-sky-900 p-1", () => new Label { Text = "p-1", AutoSize = true }),
            new Example("bg-sky-100 text-sky-900 px-4 py-2", () => new Label { Text = "px-4 py-2", AutoSize = true }),
            new Example("bg-sky-100 text-sky-900 p-6", () => new Label { Text = "p-6", AutoSize = true }),
        }),
        new Section("borders", "Borders and corners", "Anti-aliased on Tw* controls; standard controls are clipped with Region.", new[]
        {
            new Example("bg-white border border-gray-300 rounded-md", Card()),
            new Example("bg-white border-2 border-blue-500 rounded-xl", Card()),
            new Example("bg-white border-b-2 border-indigo-500", Card()),
            new Example("bg-violet-500 rounded-full", Card(120, 120)),
        }),
        new Section("shadows", "Shadows and rings", "Painted on the parent, so native controls can have them too.", new[]
        {
            new Example("bg-white rounded-lg shadow-sm", Card()),
            new Example("bg-white rounded-lg shadow-md", Card()),
            new Example("bg-white rounded-lg shadow-xl", Card()),
            new Example("bg-white rounded-lg ring-2 ring-blue-500/50 ring-offset-2", Card()),
        }),
        new Section("components", "Components", "Built-in component classes; dark: variants are included.", new[]
        {
            new Example("btn-primary", Button("Save", "save"), Themed: true),
            new Example("btn bg-white text-gray-800 border border-gray-300 dark:bg-gray-800 dark:text-gray-100 dark:border-gray-600", Button("Cancel"), "btn is the base: padding, corners, font", Themed: true),
            new Example("btn-ghost", Button("Details", "info"), Themed: true),
            new Example("btn-danger", Button("Delete", "trash"), Themed: true),
            new Example("input", () => new TwTextBox { Text = "Ayşe Yılmaz", Size = new Size(240, 38) }, Themed: true),
            new Example("card", Card(260, 120), Themed: true),
            new Example("table", () =>
            {
                var grid = new DataGridView { Size = new Size(320, 120), ColumnCount = 2, RowHeadersVisible = false, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
                grid.Columns[0].HeaderText = "Customer";
                grid.Columns[1].HeaderText = "Total";
                grid.Rows.Add("Ayşe Yılmaz", "1,250.00");
                grid.Rows.Add("Mehmet Demir", "480.50");
                return grid;
            }, Themed: true),
        }),
        new Section("layout", "Flex and grid", "On TwPanel: flex, gap, justify-*, items-*, grid-cols-*. Children are boxes with bg-indigo-500 text-white.", new[]
        {
            new Example("flex gap-2", Boxes(3, 300, 48)),
            new Example("flex justify-between", Boxes(3, 300, 48)),
            new Example("flex justify-center gap-4", Boxes(3, 300, 48)),
            new Example("grid grid-cols-3 gap-2", Boxes(6, 300, 90)),
            new Example("grid grid-cols-3 grid-rows-2 gap-2", () =>
            {
                // İlk kutu row-span-2: iki satırı kaplar, kalan kutular yanındaki hücrelere yerleşir.
                var panel = (TwPanel)Boxes(5, 300, 90)();
                panel.Controls[0].Tag = "bg-indigo-700 text-white rounded-md row-span-2";
                return panel;
            }),
        }),
        new Section("controls", "Controls", "Owner-drawn replacements for native controls.", new[]
        {
            new Example("text-gray-900 accent-blue-600 dark:text-gray-100", () => new TwSwitch { Text = "Notifications", Checked = true, AutoSize = true }, Themed: true),
            new Example("text-gray-900 accent-blue-600 dark:text-gray-100", () => new TwCheckBox { Text = "Gift wrap", Checked = true, AutoSize = true }, Themed: true),
            new Example("text-gray-900 accent-blue-600 dark:text-gray-100", () => new TwRadioButton { Text = "Standard", Checked = true, AutoSize = true }, Themed: true),
            new Example("input", () => new TwSelect { Items = { "İstanbul", "İzmir", "Ankara" }, SelectedIndex = 0, Size = new Size(200, 38) }, Themed: true),
            new Example("input", () => new TwNumberBox { Value = 12.5m, DecimalPlaces = 1, Size = new Size(160, 38) }, Themed: true),
            new Example("input", () => new TwDatePicker { Value = new DateTime(2026, 10, 15), Size = new Size(180, 38) }, Themed: true),
            new Example("fill-gray-200 accent-blue-600 dark:fill-gray-700", () => new TwSlider { Value = 60, Size = new Size(220, 28) }, Themed: true),
            new Example("fill-gray-200 accent-emerald-500 rounded-full dark:fill-gray-700", () => new TwProgress { Value = 65, Size = new Size(220, 10) }, Themed: true),
            new Example("text-gray-600 selected:text-blue-600 dark:text-gray-300", () => new TwTabStrip { Tabs = { "General", "Address", "Orders" }, Size = new Size(280, 40) }, Themed: true),
            new Example("text-gray-600 selected:bg-white selected:text-gray-900 fill-gray-100 dark:text-gray-300 dark:fill-gray-800 dark:selected:bg-gray-700 dark:selected:text-white", () => new TwTabStrip { Tabs = { "List", "Card", "Calendar" }, Variant = TwTabVariant.Segmented, Size = new Size(280, 40) }, Themed: true),
        }),
    };
}
