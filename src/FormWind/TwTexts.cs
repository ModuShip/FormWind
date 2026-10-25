using System.Globalization;

namespace FormWind;

/// <summary>
/// Texts: dialog buttons, validation messages, grid menus, screen reader names. The default language is English;
/// Turkish texts are selected when the application's UI language (<see cref="CultureInfo.CurrentUICulture"/>) is Turkish.
/// For another language, replace <see cref="Current"/>:
/// <code>TwTexts.Current = new TwTexts { Ok = "OK", Cancel = "Abbrechen", Required = "Pflichtfeld." };</code>
/// A control's own text properties (e.g. <see cref="TwField.RequiredMessage"/>) take precedence when set.
/// </summary>
public sealed class TwTexts
{
    /// <summary>The texts in use. The initial value is chosen from the application's UI language.</summary>
    public static TwTexts Current { get; set; } = ForCulture(CultureInfo.CurrentUICulture);

    /// <summary>English texts (a new instance).</summary>
    public static TwTexts English => new TwTexts();

    /// <summary>Turkish texts (a new instance).</summary>
    public static TwTexts Turkish => new TwTexts
    {
        Ok = "Tamam",
        Cancel = "İptal",
        Yes = "Evet",
        No = "Hayır",
        Close = "Kapat",
        Required = "Bu alan zorunludur.",
        InvalidFormat = "Geçersiz biçim.",
        Incomplete = "Eksik giriş.",
        MinLength = "En az {0} karakter girin.",
        InvalidEmail = "Geçerli bir e-posta adresi girin.",
        TcKimlikLength = "T.C. kimlik numarası 11 haneli olmalı.",
        InvalidTcKimlik = "Geçersiz T.C. kimlik numarası.",
        InvalidIban = "Geçersiz IBAN.",
        TurkishIbanLength = "TR IBAN 26 karakter olmalı.",
        InvalidMobile = "Geçerli bir cep telefonu girin.",
        Search = "Ara…",
        Loading = "Yükleniyor…",
        SelectAll = "(Tümünü seç)",
        ClearFilter = "Süzmeyi kaldır",
        Blank = "(Boş)",
        RangeMin = "En az",
        RangeMax = "En çok",
        Actions = "İşlemler",
        JustNow = "az önce",
        MinutesAgo = "{0} dk önce",
        HoursAgo = "{0} sa önce",
        DaysAgo = "{0} gün önce",
        ThousandSuffix = "B",
        MillionSuffix = "Mn",
        BillionSuffix = "Mr",
        Other = "Diğer",
        Total = "Toplam",
        StepCompleted = "tamamlandı",
        StepCurrent = "devam ediyor",
        StepError = "hata",
        StepPending = "bekliyor",
        Open = "Aç",
        Select = "Seç",
        Expand = "Genişlet",
        Collapse = "Daralt",
        Remove = "Kaldır",
        More = "Daha fazla",
        PreviousMonth = "Önceki ay",
        NextMonth = "Sonraki ay",
        Item = "Öğe {0}",
        Page = "Sayfa {0} / {1}",
    };

    /// <summary>Texts for a language: <see cref="Turkish"/> for Turkish, <see cref="English"/> for other languages.</summary>
    public static TwTexts ForCulture(CultureInfo culture) =>
        culture?.TwoLetterISOLanguageName == "tr" ? Turkish : English;

    // Diyalog ve bildirim düğmeleri (TwDialog, TwToast).
    public string Ok { get; set; } = "OK";
    public string Cancel { get; set; } = "Cancel";
    public string Yes { get; set; } = "Yes";
    public string No { get; set; } = "No";
    public string Close { get; set; } = "Close";

    // Doğrulama (TwField, TwValidators). {0} yer tutucuları string.Format ile doldurulur.
    public string Required { get; set; } = "This field is required.";
    public string InvalidFormat { get; set; } = "Invalid format.";
    public string Incomplete { get; set; } = "Incomplete entry.";
    /// <summary>{0}: minimum number of characters.</summary>
    public string MinLength { get; set; } = "Enter at least {0} characters.";
    public string InvalidEmail { get; set; } = "Enter a valid email address.";
    public string TcKimlikLength { get; set; } = "The Turkish ID number must have 11 digits.";
    public string InvalidTcKimlik { get; set; } = "Invalid Turkish ID number.";
    public string InvalidIban { get; set; } = "Invalid IBAN.";
    public string TurkishIbanLength { get; set; } = "A TR IBAN must have 26 characters.";
    public string InvalidMobile { get; set; } = "Enter a valid mobile number.";

    // Kontroller ve grid.
    /// <summary>Aranabilir <see cref="TwSelect"/> kutusunun ipucu.</summary>
    public string Search { get; set; } = "Search…";
    /// <summary>Text of the <see cref="TwGrid.SetLoading(System.Windows.Forms.DataGridView, bool)"/> overlay.</summary>
    public string Loading { get; set; } = "Loading…";
    /// <summary>Grid filter menu.</summary>
    public string SelectAll { get; set; } = "(Select all)";
    public string ClearFilter { get; set; } = "Clear filter";
    /// <summary>Display name of blank values in the filter menu.</summary>
    public string Blank { get; set; } = "(Blank)";
    /// <summary>Hints of the range boxes in the filter menu of number and date columns.</summary>
    public string RangeMin { get; set; } = "Min";
    public string RangeMax { get; set; } = "Max";
    /// <summary>Accessible name of the <see cref="TwActionsColumn"/> button.</summary>
    public string Actions { get; set; } = "Actions";

    // Göreli zaman (TwDateColumn.Relative). {0}: sayı.
    public string JustNow { get; set; } = "just now";
    public string MinutesAgo { get; set; } = "{0} min ago";
    public string HoursAgo { get; set; } = "{0} h ago";
    public string DaysAgo { get; set; } = "{0} d ago";

    // Grafikler: eksenlerdeki kısa sayılar (1,2K), birleştirilen dilimler ve halka grafiğin ortası.
    public string ThousandSuffix { get; set; } = "K";
    public string MillionSuffix { get; set; } = "M";
    public string BillionSuffix { get; set; } = "B";
    /// <summary>Slices past the palette folded into one (<see cref="TwDonutChart"/>).</summary>
    public string Other { get; set; } = "Other";
    public string Total { get; set; } = "Total";
    // TwStepper adımlarının durumu (ekran okuyucular).
    public string StepCompleted { get; set; } = "completed";
    public string StepCurrent { get; set; } = "current";
    public string StepError { get; set; } = "error";
    public string StepPending { get; set; } = "pending";

    // Ekran okuyucular (UI Automation adları ve varsayılan eylemler).
    public string Open { get; set; } = "Open";
    public string Select { get; set; } = "Select";
    public string Expand { get; set; } = "Expand";
    public string Collapse { get; set; } = "Collapse";
    /// <summary>The ✕ of a removable <see cref="TwChip"/>.</summary>
    public string Remove { get; set; } = "Remove";
    /// <summary>The "more" button of <see cref="TwTabStrip"/> and <see cref="TwToolbar"/> that holds what does not fit.</summary>
    public string More { get; set; } = "More";
    public string PreviousMonth { get; set; } = "Previous month";
    public string NextMonth { get; set; } = "Next month";
    /// <summary>List row without a name; {0}: position.</summary>
    public string Item { get; set; } = "Item {0}";
    /// <summary><see cref="TwPager"/>; {0}: page, {1}: page count.</summary>
    public string Page { get; set; } = "Page {0} of {1}";
}
