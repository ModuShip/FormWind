using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind;
//#if (Krypton)
using FormWind.Krypton;
//#endif

namespace FormWindApp;

/// <summary>List-detail screen: searchable customer list on the left, details on the right.</summary>
internal sealed class MainForm : Form
{
    private readonly TailwindProvider _tw = new TailwindProvider();
    private readonly TwTextBox _search = new TwTextBox { PlaceholderText = "Search customers", Size = new Size(260, 36) };
    private readonly TwSwitch _dark = new TwSwitch { Text = "Dark", AutoSize = true };
    private readonly TwDataList _list = new TwDataList { Size = new Size(320, 400), ItemHeight = 56 };
    private readonly Label _name = new Label { AutoSize = true };
    private readonly Label _email = new Label { AutoSize = true };
    private readonly Label _company = new Label { AutoSize = true };
    private readonly Label _city = new Label { AutoSize = true };
    private readonly Label _status = new Label { AutoSize = true };
    private List<Customer> _visible = Customer.Samples.ToList();
//#if (Krypton)
    // Krypton's global palette follows this form's theme and dark mode, so Krypton controls without classes match.
    private readonly IDisposable _kryptonPalette;
//#endif

    public MainForm()
    {
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Text = "FormWindApp";
//#if (Krypton)
        _kryptonPalette = TwKryptonTheme.Follow(_tw);
//#endif
        ClientSize = new Size(960, 600);
        MinimumSize = new Size(720, 480);
        StartPosition = FormStartPosition.CenterScreen;

        // Header: title, search, theme switch and settings.
        var header = new TwPanel { Dock = DockStyle.Top, Height = 72 };
        var title = new Label { Text = "Customers", AutoSize = true };
        var spacer = new Panel { Size = new Size(1, 1) };
        var settings = new TwButton { Text = "Settings", IconName = "settings", Size = new Size(120, 36) };
        settings.Click += (_, _) => OpenSettings();
        header.Controls.AddRange(new Control[] { title, spacer, _search, _dark, settings });

        // Body: list and detail card side by side.
        var body = new TwPanel { Dock = DockStyle.Fill };
        var detail = new TwPanel { Size = new Size(400, 400) };
        var detailTitle = new Label { Text = "Details", AutoSize = true };
        detail.Controls.AddRange(new Control[] { detailTitle, Row("Name", _name), Row("Email", _email), Row("Company", _company), Row("City", _city), Row("Status", _status) });
        body.Controls.AddRange(new Control[] { _list, detail });

        Controls.Add(body);
        Controls.Add(header);

        _tw.SetClasses(this, "page");
        _tw.SetClasses(header, "flex items-center gap-3 px-6 bg-white border-b border-gray-200 dark:bg-gray-900 dark:border-gray-800");
        _tw.SetClasses(title, "title");
        _tw.SetClasses(spacer, "flex-1");
        _tw.SetClasses(_search, "input");
        _tw.SetClasses(_dark, "toggle text-sm");
        _tw.SetClasses(settings, "btn-ghost");
        _tw.SetClasses(body, "flex gap-6 p-6 page");
        _tw.SetClasses(_list, "panel list w-80");
        _tw.SetClasses(detail, "panel flex flex-col gap-3 p-6 flex-1");
        _tw.SetClasses(detailTitle, "text-lg font-semibold mb-2");

        _list.DrawItem += OnDrawItem;
        _list.ItemText = i => _visible[i].Name;
        _list.SelectedIndexChanged += (_, _) => ShowDetail();
        _search.InnerTextBox.TextChanged += (_, _) => Filter();
        _dark.Checked = AppState.DarkMode;
        _dark.CheckedChanged += (_, _) => AppState.SetDarkMode(_dark.Checked);
        AppState.ThemeChanged += OnThemeChanged;

        Filter();
        OnThemeChanged(null, EventArgs.Empty);
        ResumeLayout();
    }

    // A label/value pair in the detail card.
    private Control Row(string caption, Label value)
    {
        var row = new TwPanel { Size = new Size(360, 28) };
        var label = new Label { Text = caption, AutoSize = true };
        row.Controls.AddRange(new Control[] { label, value });
        _tw.SetClasses(row, "flex items-center");
        _tw.SetClasses(label, "muted w-28");
        _tw.SetClasses(value, "text-sm font-medium");
        return row;
    }

    private void OnDrawItem(object? sender, TwDataListDrawEventArgs e)
    {
        var customer = _visible[e.Index];
        var bounds = e.ContentBounds;
        e.DrawText(customer.Name, new Rectangle(bounds.X + 8, bounds.Y + 8, bounds.Width - 16, 20), font: e.FontWithWeight(600));
        e.DrawText(customer.Company, new Rectangle(bounds.X + 8, bounds.Y + 28, bounds.Width - 16, 20), e.MutedColor);
    }

    private void Filter()
    {
        var query = _search.Text.Trim();
        _visible = Customer.Samples
            .Where(c => query.Length == 0 || c.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0
                                          || c.Company.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
            .ToList();
        _list.ItemCount = _visible.Count;
        _list.SelectedIndex = _visible.Count > 0 ? 0 : -1;
        _list.Invalidate();
        ShowDetail();
    }

    private void ShowDetail()
    {
        var customer = _list.SelectedIndex >= 0 && _list.SelectedIndex < _visible.Count ? _visible[_list.SelectedIndex] : null;
        _name.Text = customer?.Name ?? "—";
        _email.Text = customer?.Email ?? "—";
        _company.Text = customer?.Company ?? "—";
        _city.Text = customer?.City ?? "—";
        _status.Text = customer?.Status ?? "—";
        _tw.SetClasses(_status, customer?.Status switch
        {
            "Active" => "text-sm font-medium text-emerald-700 dark:text-emerald-400",
            "Pending" => "text-sm font-medium text-amber-700 dark:text-amber-400",
            _ => "text-sm font-medium text-gray-500 dark:text-gray-400",
        });
    }

    private void OpenSettings()
    {
        using var settings = new SettingsForm();
        settings.ShowDialog(this);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _tw.DarkMode = AppState.DarkMode;
        if (_dark.Checked != AppState.DarkMode) _dark.Checked = AppState.DarkMode;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AppState.ThemeChanged -= OnThemeChanged;
//#if (Krypton)
            _kryptonPalette.Dispose();
//#endif
            _tw.Dispose();
        }
        base.Dispose(disposing);
    }
}
