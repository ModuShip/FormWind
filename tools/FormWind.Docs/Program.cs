using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace FormWind.Docs;

/// <summary>
/// FormWind belge sitesini üretir: her örnek gerçek FormWind ile ekran dışında çizilir (PNG), palet ve ikonlar HTML olarak
/// yazılır. Kullanım: <c>formwind-docs [çıktı klasörü]</c> (varsayılan <c>artifacts/docs-site</c>).
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine("artifacts", "docs-site"));
        Directory.CreateDirectory(Path.Combine(output, "img"));

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var renderer = new Renderer(Path.Combine(output, "img"));
        var html = SiteWriter.Write(Catalog.Sections, renderer);
        File.WriteAllText(Path.Combine(output, "index.html"), html, new UTF8Encoding(false));
        Console.WriteLine($"{renderer.Count} görüntü, {Catalog.Sections.Count} bölüm → {output}");
        return 0;
    }
}

/// <summary>Örnekleri ekran dışındaki bir formda FormWind ile çizip PNG olarak kaydeder.</summary>
internal sealed class Renderer
{
    // Gölgelerin (shadow-xl: 25 px bulanıklık + 20 px kayma) sığacağı kenar payı.
    private const int Margin = 48;
    private readonly string _folder;

    public Renderer(string folder) => _folder = folder;

    public int Count { get; private set; }

    /// <returns>Görüntünün site köküne göre yolu.</returns>
    public string Render(Example example, string name, bool dark)
    {
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-10000, -10000),
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            AutoScaleMode = AutoScaleMode.None,
        };
        using var provider = new TailwindProvider { DarkMode = dark };
        var control = example.Make();
        var stage = new TwPanel { Location = Point.Empty, Size = new Size(control.Width + Margin * 2, control.Height + Margin * 2) };
        control.Location = new Point(Margin, Margin);
        stage.Controls.Add(control);
        form.ClientSize = stage.Size;
        form.Controls.Add(stage);
        // Odak örnekten alınıp görünmez bir düğmeye verilir (focus: stilleri ve seçili metin görünmesin).
        var focusSink = new Button { Size = Size.Empty, Location = new Point(-10, -10), TabIndex = 0 };
        form.Controls.Add(focusSink);

        provider.SetClasses(stage, "bg-white dark:bg-gray-950");
        foreach (var child in Descendants(control))
            if (child.Tag is string classes) provider.SetClasses(child, classes);
        provider.SetClasses(control, example.Classes);
        form.Show();
        // Örnekler odaksız ve seçimsiz çizilir (TextBox metni seçili, grid'in ilk hücresi seçili görünmesin).
        focusSink.Focus();
        if (control is DataGridView grid) grid.ClearSelection();
        Application.DoEvents();

        // AutoSize kontroller class'larla büyüyebilir; sahne yeni boyuta göre yeniden ayarlanır.
        stage.Size = new Size(control.Width + Margin * 2, control.Height + Margin * 2);
        form.ClientSize = stage.Size;
        Application.DoEvents();

        using var bitmap = new Bitmap(stage.Width, stage.Height);
        stage.DrawToBitmap(bitmap, new Rectangle(Point.Empty, stage.Size));
        var file = name + (dark ? "-dark" : "") + ".png";
        bitmap.Save(Path.Combine(_folder, file), ImageFormat.Png);
        Count++;
        form.Close();
        return "img/" + file;
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}

/// <summary>Tek sayfalık statik siteyi yazar (gömülü CSS, açık/koyu tema sistem ayarına göre).</summary>
internal static class SiteWriter
{
    public static string Write(IReadOnlyList<Section> sections, Renderer renderer)
    {
        var html = new StringBuilder();
        html.Append(Head);
        html.Append("<nav><a class=\"back\" href=\"../docs/index.html\">← Documentation</a><h1>FormWind</h1><p>Tailwind-style utility classes for WinForms</p><ul>");
        foreach (var section in sections) html.Append($"<li><a href=\"#{section.Id}\">{E(section.Title)}</a></li>");
        html.Append("<li><a href=\"#palette\">Color palette</a></li><li><a href=\"#icons\">Icons</a></li></ul></nav><main>");
        html.Append("<p class=\"lead\">Every preview below is rendered by FormWind itself on a real WinForms control. "
                    + "See the <a href=\"https://github.com/ModuShip/FormWind\">README</a> for the full class reference.</p>");

        foreach (var section in sections)
        {
            html.Append($"<section id=\"{section.Id}\"><h2>{E(section.Title)}</h2><p>{E(section.Intro)}</p><table>");
            html.Append("<tr><th>Classes</th><th>Preview</th></tr>");
            for (int i = 0; i < section.Examples.Count; i++)
            {
                var example = section.Examples[i];
                var name = $"{section.Id}-{i + 1}";
                html.Append($"<tr><td><code>{E(example.Classes)}</code>");
                if (example.Note != null) html.Append($"<div class=\"note\">{E(example.Note)}</div>");
                html.Append("</td><td class=\"preview\">");
                html.Append($"<img src=\"{renderer.Render(example, name, dark: false)}\" alt=\"{E(example.Classes)}\">");
                if (example.Themed) html.Append($"<img src=\"{renderer.Render(example, name, dark: true)}\" alt=\"{E(example.Classes)} (dark)\">");
                html.Append("</td></tr>");
            }
            html.Append("</table></section>");
        }

        html.Append(Palette());
        html.Append(Icons());
        html.Append("</main></body></html>");
        return html.ToString();
    }

    private static string Palette()
    {
        var html = new StringBuilder("<section id=\"palette\"><h2>Color palette</h2>"
            + "<p>22 colors × 11 shades. Tailwind v3 is the default; v4 (OKLCH, gamut-mapped to sRGB) is selected with "
            + "<code>\"palette\": \"v4\"</code>.</p>");
        foreach (var (title, theme) in new[] { ("Tailwind v3", new TwTheme()), ("Tailwind v4", new TwTheme(TwPaletteVersion.V4)) })
        {
            html.Append($"<h3>{title}</h3><div class=\"palette\">");
            foreach (var name in TwPalette.Colors.Keys)
            {
                html.Append($"<div class=\"row\"><span class=\"name\">{name}</span>");
                foreach (var shade in TwPalette.Shades)
                {
                    theme.TryGetColor(name, shade, out var color);
                    var hex = $"#{color.R:x2}{color.G:x2}{color.B:x2}";
                    html.Append($"<span class=\"swatch\" style=\"background:{hex}\" title=\"{name}-{shade} {hex}\"></span>");
                }
                html.Append("</div>");
            }
            html.Append("</div>");
        }
        return html.Append("</section>").ToString();
    }

    private static string Icons()
    {
        var html = new StringBuilder("<section id=\"icons\"><h2>Icons</h2><p>Built-in names for <code>TwIcon.IconName</code> and "
            + "<code>TwButton.IconName</code> (Segoe Fluent Icons on Windows 11). Any glyph can be used by code point (<code>#E8A5</code>) "
            + "and SVG icons can replace them in the theme file.</p><ul class=\"icons\">");
        foreach (var name in TwIcons.Names) html.Append($"<li><code>{E(name)}</code></li>");
        return html.Append("</ul></section>").ToString();
    }

    private static string E(string text) => WebUtility.HtmlEncode(text);

    private const string Head = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>FormWind class reference</title>
<style>
:root { --bg: #ffffff; --fg: #111827; --muted: #6b7280; --line: #e5e7eb; --code: #f3f4f6; --accent: #2563eb; }
@media (prefers-color-scheme: dark) { :root { --bg: #030712; --fg: #f3f4f6; --muted: #9ca3af; --line: #1f2937; --code: #111827; --accent: #60a5fa; } }
* { box-sizing: border-box; }
body { margin: 0; background: var(--bg); color: var(--fg); font: 15px/1.6 "Segoe UI", system-ui, sans-serif; display: flex; }
nav { position: sticky; top: 0; height: 100vh; overflow: auto; width: 240px; flex: none; padding: 24px; border-right: 1px solid var(--line); }
nav h1 { margin: 0; font-size: 22px; } nav p { margin: 4px 0 16px; color: var(--muted); font-size: 13px; }
nav ul { list-style: none; padding: 0; margin: 0; } nav a { display: block; padding: 4px 0; color: var(--fg); text-decoration: none; }
nav a:hover { color: var(--accent); } nav a.back { color: var(--muted); font-size: 13px; margin-bottom: 12px; }
main { flex: 1; min-width: 0; padding: 24px 40px 80px; max-width: 1100px; }
a { color: var(--accent); } .lead { color: var(--muted); }
h2 { margin-top: 48px; padding-top: 8px; border-top: 1px solid var(--line); }
table { border-collapse: collapse; width: 100%; } th { text-align: left; color: var(--muted); font-weight: 600; font-size: 13px; }
td, th { padding: 10px 12px; border-bottom: 1px solid var(--line); vertical-align: middle; }
td:first-child { width: 40%; } code { background: var(--code); padding: 2px 6px; border-radius: 4px; font-size: 13px; word-break: break-word; }
.note { color: var(--muted); font-size: 13px; margin-top: 4px; }
.preview img { margin: 4px 8px 4px 0; border-radius: 8px; border: 1px solid var(--line); vertical-align: middle; max-width: 100%; }
.palette .row { display: flex; align-items: center; gap: 4px; margin: 2px 0; } .palette .name { width: 72px; font-size: 13px; color: var(--muted); }
.swatch { flex: 1; height: 28px; border-radius: 4px; min-width: 0; }
.icons { list-style: none; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 6px; }
@media (max-width: 760px) { body { display: block; } nav { position: static; height: auto; width: auto; border-right: 0; border-bottom: 1px solid var(--line); } main { padding: 16px; } td:first-child { width: auto; } }
</style>
</head>
<body>
""";
}
