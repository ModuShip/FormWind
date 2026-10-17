using System.Windows.Forms;
using FormWind;

namespace DurusMerkezi
{
    /// <summary>Kenar çubuğunun ekrana özgü bölümündeki bir satır: renkli nokta, metin ve sağda bir değer.</summary>
    internal readonly struct SideItem
    {
        public SideItem(string text, Tone? dot, string value)
        {
            Text = text;
            Dot = dot;
            Value = value;
        }

        public string Text { get; }
        public Tone? Dot { get; }
        public string Value { get; }
    }

    /// <summary>
    /// Bir ekran: araç çubuğundaki başlık ve kontroller, kenar çubuğundaki bölümü ve kaydırılan içerik. İçerik ilk
    /// gösterildiğinde bir kez kurulur.
    /// </summary>
    internal abstract class Page
    {
        private TwPanel _content;
        private Control[] _toolbar;

        protected Page(Ui ui) => Ui = ui;

        protected Ui Ui { get; }

        public abstract string Title { get; }

        public abstract string Subtitle { get; }

        public abstract string SideTitle { get; }

        public abstract SideItem[] SideItems { get; }

        /// <summary>Kenar çubuğunun altındaki son veri satırı.</summary>
        public virtual string Footer => "Son veri: Vardiya B kapanışı · 00:12";

        public virtual Tone FooterTone => Tone.Green;

        public Control[] Toolbar => _toolbar ??= BuildToolbar();

        /// <summary>Araç çubuğunun ikinci satırı; <c>null</c> = yok.</summary>
        public virtual Control[] SecondaryToolbar => null;

        public TwPanel Content => _content ??= BuildContent();

        protected abstract Control[] BuildToolbar();

        protected abstract TwPanel BuildContent();

        /// <summary>Sayfa iskeleti: başlık, alt başlık ve sağdaki rozetler; altında satırlar.</summary>
        protected TwPanel PageBody(string title, string subtitle, Control[] badges, params Control[] rows)
        {
            var heading = Ui.Col("gap-1", Ui.Text(title, "h1"), Ui.Text(subtitle, "muted"));
            var top = Ui.Row("items-end", heading, Ui.Spacer());
            top.Controls.AddRange(badges);
            var body = Ui.Box("flex flex-col gap-4 px-6 pt-5 pb-8", top);
            body.Controls.AddRange(rows);
            body.AutoSize = true;
            return body;
        }
    }
}
