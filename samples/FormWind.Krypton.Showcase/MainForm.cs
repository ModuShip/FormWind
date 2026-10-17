using System;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Krypton;
using Krypton.Toolkit;

namespace FormWind.KryptonShowcase;

/// <summary>
/// Krypton kontrolleriyle kurulu bir form. Kontrollerin çoğunun class'ı yok: tema köprüsü (<see cref="TwKryptonTheme.Follow"/>)
/// Krypton'un genel paletini seçili FormWind görünümünden üretir; görünüm ya da koyu tema değişince palet yeniden kurulur.
/// Vurgulu birkaç kontrol (kaydet butonu, uyarı, sekme butonları) kendi class'larını alır (<see cref="KryptonApplier"/>).
/// </summary>
internal sealed class MainForm : KryptonForm
{
    private static readonly (string Text, TwPreset Preset)[] Presets =
    {
        ("Tailwind", TwPreset.Tailwind), ("Fluent", TwPreset.Fluent), ("Ant Design", TwPreset.Antd), ("Material", TwPreset.Material),
    };

    private readonly TailwindProvider _tw = new TailwindProvider();
    private readonly IDisposable _palette;
    private readonly KryptonCheckBox _dark;
    private readonly KryptonPanel _root;

    // SuspendLayout/ResumeLayout kullanılmaz: o durumda ekran dışında açılan KryptonForm'un (--snapshot) alt kontrolleri
    // oluşmuyor (FormWind olmadan da; Krypton 100/105).
    public MainForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "FormWind + Krypton";
        ClientSize = new Size(820, 540);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        _root = new KryptonPanel { Dock = DockStyle.Fill };
        var root = _root;
        Controls.Add(root);

        var title = new KryptonLabel { Text = "Müşteriler", Location = new Point(20, 16), LabelStyle = LabelStyle.TitlePanel };
        var preset = new KryptonComboBox { Location = new Point(520, 20), Size = new Size(150, 26), DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var item in Presets) preset.Items.Add(item.Text);
        preset.SelectedIndex = Math.Max(0, Array.FindIndex(Presets, p => p.Preset == TwTheme.Current.Preset));
        preset.SelectedIndexChanged += (_, _) => TwTheme.Current = new TwTheme(Presets[preset.SelectedIndex].Preset);
        _dark = new KryptonCheckBox { Text = "Koyu", Location = new Point(690, 22) };
        _dark.CheckedChanged += (_, _) => _tw.DarkMode = _dark.Checked;

        // Sol: başlık grubu içinde form alanları.
        var details = new KryptonHeaderGroup { Location = new Point(20, 64), Size = new Size(360, 450) };
        details.ValuesPrimary.Heading = "Müşteri";
        details.ValuesPrimary.Image = null;
        details.ValuesSecondary.Heading = "Son güncelleme: 08.10.2026";
        var body = details.Panel;
        body.Controls.Add(new KryptonLabel { Text = "Ad soyad", Location = new Point(16, 16) });
        body.Controls.Add(new KryptonTextBox { Text = "Ayşe Yılmaz", Location = new Point(16, 40), Size = new Size(320, 28) });
        body.Controls.Add(new KryptonLabel { Text = "Şehir", Location = new Point(16, 80) });
        var city = new KryptonComboBox { Location = new Point(16, 104), Size = new Size(150, 26), DropDownStyle = ComboBoxStyle.DropDownList };
        city.Items.AddRange(new object[] { "İstanbul", "Ankara", "İzmir" });
        city.SelectedIndex = 0;
        body.Controls.Add(city);
        body.Controls.Add(new KryptonLabel { Text = "Kayıt tarihi", Location = new Point(186, 80) });
        body.Controls.Add(new KryptonDateTimePicker { Location = new Point(186, 104), Size = new Size(150, 26), Value = new DateTime(2026, 10, 15) });
        body.Controls.Add(new KryptonCheckBox { Text = "Aktif müşteri", Checked = true, Location = new Point(16, 148) });
        var segment = new KryptonPanel { Location = new Point(16, 184), Size = new Size(320, 32) };
        var segments = new[] { "Bireysel", "Kurumsal", "Bayi" };
        for (int i = 0; i < segments.Length; i++)
        {
            var button = new KryptonCheckButton { Text = segments[i], Checked = i == 0, Location = new Point(i * 108, 0), Size = new Size(104, 32) };
            button.Click += (sender, _) => { foreach (var other in segment.Controls) if (other is KryptonCheckButton b) b.Checked = b == sender; };
            segment.Controls.Add(button);
            _tw.SetClasses(button, "btn-ghost checked:bg-primary-600 checked:text-white checked:border-primary-600 dark:checked:bg-primary-600");
        }
        body.Controls.Add(segment);
        // Krypton etiketi saydamdır; zeminli uyarı için standart Label (FormWind'in WinForms uygulayıcısı).
        var notice = new Label { Text = "Değişiklikler kaydedildi.", Location = new Point(16, 232), AutoSize = false, Size = new Size(320, 40), TextAlign = ContentAlignment.MiddleLeft };
        body.Controls.Add(notice);
        var save = new KryptonButton { Text = "Kaydet", Location = new Point(16, 340), Size = new Size(100, 34) };
        var cancel = new KryptonButton { Text = "İptal", Location = new Point(126, 340), Size = new Size(100, 34) };
        body.Controls.Add(save);
        body.Controls.Add(cancel);

        // Sağ: tablo ve liste, class'sız (paletten).
        var grid = new KryptonDataGridView
        {
            Location = new Point(400, 64), Size = new Size(400, 220), AllowUserToAddRows = false, ReadOnly = true, RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        grid.Columns.Add("no", "NO");
        grid.Columns.Add("date", "TARİH");
        grid.Columns.Add("amount", "TUTAR");
        grid.Rows.Add("#10234", "08.10.2026", "1.250,00 ₺");
        grid.Rows.Add("#10233", "07.10.2026", "480,50 ₺");
        grid.Rows.Add("#10232", "05.10.2026", "2.940,00 ₺");
        var group = new KryptonGroupBox { Text = "Notlar", Location = new Point(400, 300), Size = new Size(400, 214) };
        var notes = new KryptonListBox { Dock = DockStyle.Fill };
        notes.Items.AddRange(new object[] { "Teslimat adresi güncellendi", "İndirim kodu tanımlandı", "Fatura e-postayla gönderildi" });
        notes.SelectedIndex = 1;
        group.Panel.Controls.Add(notes);

        root.Controls.AddRange(new Control[] { title, preset, _dark, details, grid, group });

        _tw.SetClasses(save, "btn-primary");
        _tw.SetClasses(notice, "alert-success");
        _palette = TwKryptonTheme.Follow(_tw);
    }

    public bool DarkMode
    {
        get => _dark.Checked;
        set => _dark.Checked = value;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _palette.Dispose();
            _tw.Dispose();
        }
        base.Dispose(disposing);
    }
}
