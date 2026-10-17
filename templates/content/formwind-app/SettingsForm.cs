using System;
using System.Drawing;
using System.Windows.Forms;
using FormWind;

namespace FormWindApp;

/// <summary>Settings screen: grouped options with Save / Cancel.</summary>
internal sealed class SettingsForm : Form
{
    private readonly TailwindProvider _tw = new TailwindProvider();
    private readonly TwTextBox _name = new TwTextBox { Size = new Size(360, 38) };
    private readonly TwSelect _language = new TwSelect { Items = { "English", "Türkçe", "Deutsch" }, SelectedIndex = 0, Size = new Size(200, 38) };
    private readonly TwSwitch _dark = new TwSwitch { Text = "Dark theme", AutoSize = true };
    private readonly TwCheckBox _notifications = new TwCheckBox { Text = "Email notifications", AutoSize = true };

    public SettingsForm()
    {
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Text = "Settings";
        ClientSize = new Size(480, 500);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var page = new TwPanel { Dock = DockStyle.Fill };
        var title = new Label { Text = "Settings", AutoSize = true };
        var profile = new Label { Text = "PROFILE", AutoSize = true };
        var nameField = new TwField { LabelText = "Display name", Required = true };
        nameField.Controls.Add(_name);
        var languageField = new TwField { LabelText = "Language" };
        languageField.Controls.Add(_language);
        var appearance = new Label { Text = "APPEARANCE", AutoSize = true };
        var spacer = new Panel { Size = new Size(1, 1) };
        var actions = new TwPanel { Size = new Size(400, 40) };
        var cancel = new TwButton { Text = "Cancel", Size = new Size(100, 38), DialogResult = DialogResult.Cancel };
        var save = new TwButton { Text = "Save", Size = new Size(100, 38) };
        save.Click += OnSave;
        actions.Controls.AddRange(new Control[] { cancel, save });
        AcceptButton = save;
        CancelButton = cancel;

        page.Controls.AddRange(new Control[] { title, profile, nameField, languageField, appearance, _dark, _notifications, spacer, actions });
        Controls.Add(page);

        _tw.SetClasses(this, "page");
        _tw.SetClasses(page, "flex flex-col gap-3 p-8 page");
        _tw.SetClasses(title, "title mb-2");
        _tw.SetClasses(profile, "text-xs font-semibold text-gray-500 dark:text-gray-400");
        _tw.SetClasses(appearance, "text-xs font-semibold text-gray-500 mt-2 dark:text-gray-400");
        _tw.SetClasses(nameField, "field");
        _tw.SetClasses(languageField, "field");
        _tw.SetClasses(_name, "input");
        _tw.SetClasses(_language, "input");
        _tw.SetClasses(_dark, "toggle");
        _tw.SetClasses(_notifications, "toggle");
        _tw.SetClasses(spacer, "flex-1");
        _tw.SetClasses(actions, "flex justify-end gap-2");
        _tw.SetClasses(cancel, "btn-ghost");
        _tw.SetClasses(save, "btn-primary");

        _name.Text = AppState.UserName;
        _dark.Checked = AppState.DarkMode;
        _notifications.Checked = AppState.Notifications;
        _dark.CheckedChanged += (_, _) => _tw.DarkMode = _dark.Checked;   // live preview
        _tw.DarkMode = AppState.DarkMode;
        ResumeLayout();
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (!_tw.Validate(this)) return;
        AppState.UserName = _name.Text.Trim();
        AppState.Notifications = _notifications.Checked;
        AppState.SetDarkMode(_dark.Checked);
        DialogResult = DialogResult.OK;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tw.Dispose();
        base.Dispose(disposing);
    }
}
