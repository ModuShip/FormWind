using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using System.Linq;
using System.Windows.Forms;

namespace FormWind.Design;

/// <summary>
/// Designer support for <see cref="TailwindProvider"/>: adds a FormWind section to the smart tag of every control on the form
/// (standard controls included). The section edits the classes and applies components suited to the control type (button →
/// <c>btn-primary</c>, text box → <c>input</c>, panel → <c>card</c>, grid → <c>table</c>) with one click. Changes go through the
/// extender property, so they are written to Designer.cs and can be undone.
/// <para>
/// The preview (<see cref="TailwindProvider.DesignTimePreview"/>) applies the classes' colors, fonts and spacing to the control;
/// so that these values are not written to Designer.cs, a type descriptor is added per instance to every control: properties
/// managed by the classes (BackColor, ForeColor, Font, Padding, Margin, Cursor) are not serialized.
/// </para>
/// <para>
/// The provider gets the theme of the project being designed (<see cref="TwDesignTheme"/>), so the preview and the lists use
/// the project's colors and components. Everything here (smart tag, class builder, drop-downs) works in the out-of-process
/// (.NET 8+) Designer as well; only the "…" dialog (<see cref="ClassesEditor"/>) is limited to the .NET Framework Designer.
/// </para>
/// </summary>
internal class TailwindProviderDesigner : ComponentDesigner
{
    private readonly Dictionary<IComponent, DesignerActionListCollection> _lists = new Dictionary<IComponent, DesignerActionListCollection>();
    private readonly Dictionary<Control, PreviewTypeProvider> _previews = new Dictionary<Control, PreviewTypeProvider>();
    private DesignerActionService? _actions;
    private IComponentChangeService? _changes;

    private TailwindProvider Provider => (TailwindProvider)Component;

    public override void Initialize(IComponent component)
    {
        base.Initialize(component);
        LoadProjectTheme();
        _actions = GetService(typeof(DesignerActionService)) as DesignerActionService;
        _changes = GetService(typeof(IComponentChangeService)) as IComponentChangeService;
        if (GetService(typeof(IDesignerHost)) is IDesignerHost host)
            foreach (IComponent existing in host.Container.Components) Attach(existing);
        if (_changes != null)
        {
            _changes.ComponentAdded += OnComponentAdded;
            _changes.ComponentRemoved += OnComponentRemoved;
        }
    }

    // Tema her tasarımcıda açıkça atanır: başka bir projenin tasarımcısının değiştirdiği TwTheme.Current'a bağlı kalınmaz.
    // Current da projenin temasına çekilir; ikonlar ve grid kolonları gibi sağlayıcıya bağlı olmayan çizimler onu kullanır
    // (aynı süreçte birden çok proje açıksa son yüklenen form kazanır).
    private void LoadProjectTheme()
    {
        var theme = TwDesignTheme.Find(GetService);
        Provider.Theme = theme ?? new TwTheme();
        if (theme != null) TwTheme.Current = theme;
    }

    private void OnComponentAdded(object? sender, ComponentEventArgs e)
    {
        if (e.Component != null) Attach(e.Component);
    }

    private void OnComponentRemoved(object? sender, ComponentEventArgs e)
    {
        if (e.Component is Control control && _previews.TryGetValue(control, out var preview))
        {
            TypeDescriptor.RemoveProvider(preview, control);
            _previews.Remove(control);
        }
        if (e.Component is null || !_lists.TryGetValue(e.Component, out var lists)) return;
        foreach (DesignerActionList list in lists) _actions?.Remove(e.Component, list);
        _lists.Remove(e.Component);
    }

    private void Attach(IComponent component)
    {
        if (component is not Control control || !Provider.CanExtend(control)) return;
        if (!_previews.ContainsKey(control))
        {
            var preview = new PreviewTypeProvider(TypeDescriptor.GetProvider(control), Provider);
            TypeDescriptor.AddProvider(preview, control);
            _previews[control] = preview;
        }
        if (_actions is null || _lists.ContainsKey(component)) return;
        var lists = new DesignerActionListCollection { new ClassesActionList(Provider, control) };
        _lists[component] = lists;
        _actions.Add(component, lists);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_changes != null)
            {
                _changes.ComponentAdded -= OnComponentAdded;
                _changes.ComponentRemoved -= OnComponentRemoved;
            }
            foreach (var pair in _lists)
                foreach (DesignerActionList list in pair.Value) _actions?.Remove(pair.Key, list);
            _lists.Clear();
            foreach (var pair in _previews) TypeDescriptor.RemoveProvider(pair.Value, pair.Key);
            _previews.Clear();
        }
        base.Dispose(disposing);
    }

    /// <summary>Type descriptor that does not serialize the class-managed properties applied by the preview.</summary>
    private sealed class PreviewTypeProvider : TypeDescriptionProvider
    {
        private readonly TailwindProvider _provider;

        public PreviewTypeProvider(TypeDescriptionProvider parent, TailwindProvider provider) : base(parent) => _provider = provider;

        public override ICustomTypeDescriptor? GetTypeDescriptor(Type objectType, object? instance)
        {
            var descriptor = base.GetTypeDescriptor(objectType, instance);
            return instance is Control control && descriptor != null ? new Descriptor(descriptor, _provider, control) : descriptor;
        }

        private sealed class Descriptor : CustomTypeDescriptor
        {
            private readonly TailwindProvider _provider;
            private readonly Control _control;

            public Descriptor(ICustomTypeDescriptor parent, TailwindProvider provider, Control control) : base(parent)
            {
                _provider = provider;
                _control = control;
            }

            public override PropertyDescriptorCollection GetProperties() => Wrap(base.GetProperties());

            public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes) => Wrap(base.GetProperties(attributes));

            private PropertyDescriptorCollection Wrap(PropertyDescriptorCollection properties)
            {
                var wrapped = new PropertyDescriptor[properties.Count];
                for (int i = 0; i < properties.Count; i++)
                    wrapped[i] = IsStyleProperty(properties[i].Name) ? new ManagedProperty(properties[i], _provider, _control) : properties[i];
                return new PropertyDescriptorCollection(wrapped, true);
            }
        }

        private static bool IsStyleProperty(string name) =>
            name is nameof(Control.BackColor) or nameof(Control.ForeColor) or nameof(Control.Font)
                or nameof(Control.Padding) or nameof(Control.Margin) or nameof(Control.Cursor);
    }

    /// <summary>If the classes manage this property (and the preview is on), its value is not written to Designer.cs.</summary>
    private sealed class ManagedProperty : PropertyDescriptor
    {
        private readonly PropertyDescriptor _inner;
        private readonly TailwindProvider _provider;
        private readonly Control _control;

        public ManagedProperty(PropertyDescriptor inner, TailwindProvider provider, Control control) : base(inner)
        {
            _inner = inner;
            _provider = provider;
            _control = control;
        }

        internal static bool IsManaged(TailwindProvider provider, Control control, string property)
        {
            if (!provider.DesignTimePreview || provider.GetParsed(control) is not TwParsed parsed) return false;
            var style = parsed.Resolve(provider.BaseState);
            return property switch
            {
                nameof(Control.BackColor) => style.BackColor.HasValue,
                nameof(Control.ForeColor) => style.ForeColor.HasValue,
                nameof(Control.Font) => style.HasFont,
                nameof(Control.Padding) => !style.Padding.IsEmpty,
                nameof(Control.Margin) => !style.Margin.IsEmpty,
                nameof(Control.Cursor) => style.Cursor != null,
                _ => false,
            };
        }

        public override bool ShouldSerializeValue(object component) =>
            !IsManaged(_provider, _control, Name) && _inner.ShouldSerializeValue(component);

        public override Type ComponentType => _inner.ComponentType;
        public override bool IsReadOnly => _inner.IsReadOnly;
        public override Type PropertyType => _inner.PropertyType;
        public override bool CanResetValue(object component) => _inner.CanResetValue(component);
        public override object? GetValue(object? component) => _inner.GetValue(component);
        public override void ResetValue(object component) => _inner.ResetValue(component);
        public override void SetValue(object? component, object? value) => _inner.SetValue(component, value);
        public override TypeConverter Converter => _inner.Converter;
    }

    /// <summary>
    /// The FormWind section in a control's smart tag: the class string, the class builder (one field per group such as
    /// background, padding, corners and hover background; each edits only its own classes), component shortcuts and clearing.
    /// </summary>
    internal sealed class ClassesActionList : DesignerActionList
    {
        private const string Section = "FormWind";
        private const string ColorsSection = "Colors";
        private const string ShapeSection = "Spacing and shape";
        private const string TypographySection = "Typography";
        private const string StatesSection = "Hover and dark mode";

        private readonly TailwindProvider _provider;
        private readonly Control _control;

        public ClassesActionList(TailwindProvider provider, Control control) : base(control)
        {
            _provider = provider;
            _control = control;
        }

        internal TailwindProvider Provider => _provider;

        [Editor(typeof(ClassesEditor), typeof(UITypeEditor))]
        [TypeConverter(typeof(ClassesConverter))]
        public string Classes
        {
            get => _provider.GetClasses(_control);
            set
            {
                ClassesProperty().SetValue(_control, value ?? "");
                // Diğer alanlar (ve "Clear classes") aynı dizgeden okunur; panel yeniden çizilir.
                (GetService(typeof(DesignerActionUIService)) as DesignerActionUIService)?.Refresh(_control);
            }
        }

        [TypeConverter(typeof(TwComponentValues))]
        public string ThemeComponent
        {
            get => TwClassCategories.Components(_provider.Theme, EffectiveClasses);
            set => Classes = TwClassCategories.WithComponent(_provider.Theme, Classes, value);
        }

        [TypeConverter(typeof(TwBackgroundValues))]
        public string Background { get => Get(TwClassCategories.Background); set => Set(TwClassCategories.Background, value); }

        [TypeConverter(typeof(TwTextColorValues))]
        public string TextColor { get => Get(TwClassCategories.TextColor); set => Set(TwClassCategories.TextColor, value); }

        [TypeConverter(typeof(TwBorderValues))]
        public string Border { get => Get(TwClassCategories.Border); set => Set(TwClassCategories.Border, value); }

        [TypeConverter(typeof(TwBorderColorValues))]
        public string BorderColor { get => Get(TwClassCategories.BorderColor); set => Set(TwClassCategories.BorderColor, value); }

        [TypeConverter(typeof(TwPaddingValues))]
        public string Padding { get => Get(TwClassCategories.Padding); set => Set(TwClassCategories.Padding, value); }

        [TypeConverter(typeof(TwCornersValues))]
        public string Corners { get => Get(TwClassCategories.Corners); set => Set(TwClassCategories.Corners, value); }

        [TypeConverter(typeof(TwTextSizeValues))]
        public string TextSize { get => Get(TwClassCategories.TextSize); set => Set(TwClassCategories.TextSize, value); }

        [TypeConverter(typeof(TwFontWeightValues))]
        public string FontWeight { get => Get(TwClassCategories.FontWeight); set => Set(TwClassCategories.FontWeight, value); }

        [TypeConverter(typeof(TwHoverBackgroundValues))]
        public string HoverBackground { get => Get(TwClassCategories.HoverBackground); set => Set(TwClassCategories.HoverBackground, value); }

        [TypeConverter(typeof(TwDarkBackgroundValues))]
        public string DarkBackground { get => Get(TwClassCategories.DarkBackground); set => Set(TwClassCategories.DarkBackground, value); }

        [TypeConverter(typeof(TwDarkTextColorValues))]
        public string DarkTextColor { get => Get(TwClassCategories.DarkTextColor); set => Set(TwClassCategories.DarkTextColor, value); }

        // Alanlar AutoStyle'ın bileşeni dahil uygulanan class'ları gösterir; yazarken yalnızca kontrolün kendi class'ları değişir
        // (bg-red-50 seçmek otomatik "input"un üzerine eklenir).
        private string EffectiveClasses => _provider.GetAutomaticClasses(_control) is string automatic ? (automatic + " " + Classes).Trim() : Classes;

        private string Get(TwClassCategory category) => category.Get(EffectiveClasses);

        private void Set(TwClassCategory category, string? value)
        {
            var classes = category.Set(Classes, value);
            if (classes != Classes) Classes = classes;
        }

        /// <summary>Components suited to the control type (including those in the theme file, when the theme has them).</summary>
        internal IEnumerable<string> SuggestedComponents()
        {
            string[] candidates = _control switch
            {
                ButtonBase => new[] { "btn-primary", "btn-ghost", "btn-danger" },
                TextBoxBase or TwTextBox or TwSelect or TwNumberBox or TwDatePicker => new[] { "input" },
                DataGridView => new[] { "table" },
                Panel or GroupBox or UserControl => new[] { "card" },
                _ => Array.Empty<string>(),
            };
            return candidates.Where(c => _provider.Theme.Components.ContainsKey(c));
        }

        /// <summary>Replaces the control's component and keeps its utilities.</summary>
        internal void Apply(string component) => ThemeComponent = component;

        public void Clear() => Classes = "";

        // Extender özelliği: başka bir provider varsa onunkiyle karışmasın diye bu provider'ınki seçilir.
        private PropertyDescriptor ClassesProperty() =>
            TypeDescriptor.GetProperties(_control).Cast<PropertyDescriptor>().First(p =>
                p.Name == "Classes"
                && p.Attributes[typeof(ExtenderProvidedPropertyAttribute)] is ExtenderProvidedPropertyAttribute { Provider: var owner }
                && ReferenceEquals(owner, _provider));

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            var items = new DesignerActionItemCollection
            {
                new DesignerActionHeaderItem(Section),
                new DesignerActionPropertyItem(nameof(Classes), "Classes", Section, "Tailwind-style classes (e.g. bg-blue-600 text-white px-4 py-2)."),
                new DesignerActionPropertyItem(nameof(ThemeComponent), "Component", Section, "Component from the theme (btn-primary, card …); the other classes are kept."),
            };
            foreach (var component in SuggestedComponents())
                items.Add(new ComponentItem(this, component));
            if (Classes.Length > 0)
                items.Add(new DesignerActionMethodItem(this, nameof(Clear), "Clear classes", Section, true));
            if (_provider.GetAutomaticClasses(_control) is string automatic)
                items.Add(new DesignerActionTextItem("AutoStyle: " + automatic, Section));
            // ClassesEditor'ün uyarısı; metin satırı süreç dışı Designer'da da görünür.
            var unknown = _provider.Theme.Parse(Classes).Unknown;
            if (unknown.Count > 0)
                items.Add(new DesignerActionTextItem("Unknown classes: " + string.Join(", ", unknown), Section));

            items.Add(new DesignerActionHeaderItem(ColorsSection));
            items.Add(Field(nameof(Background), "Background", ColorsSection, "bg-*"));
            items.Add(Field(nameof(TextColor), "Text color", ColorsSection, "text-*"));
            items.Add(Field(nameof(BorderColor), "Border color", ColorsSection, "border-* (color)"));

            items.Add(new DesignerActionHeaderItem(ShapeSection));
            items.Add(Field(nameof(Padding), "Padding", ShapeSection, "p-*, px-*, py-* …"));
            items.Add(Field(nameof(Border), "Border", ShapeSection, "border, border-2, border-b …"));
            items.Add(Field(nameof(Corners), "Corners", ShapeSection, "rounded-*"));

            items.Add(new DesignerActionHeaderItem(TypographySection));
            items.Add(Field(nameof(TextSize), "Text size", TypographySection, "text-xs … text-4xl"));
            items.Add(Field(nameof(FontWeight), "Font weight", TypographySection, "font-thin … font-black"));

            items.Add(new DesignerActionHeaderItem(StatesSection));
            items.Add(Field(nameof(HoverBackground), "Hover background", StatesSection, "hover:bg-*"));
            items.Add(Field(nameof(DarkBackground), "Dark background", StatesSection, "dark:bg-*"));
            items.Add(Field(nameof(DarkTextColor), "Dark text color", StatesSection, "dark:text-*"));
            return items;
        }

        private static DesignerActionPropertyItem Field(string property, string name, string section, string classes) =>
            new DesignerActionPropertyItem(property, name, section,
                $"Sets the {classes} classes; the other classes are kept. Leave it empty to remove them.");

        // Bileşen başına bir komut; DesignerActionMethodItem yalnızca parametresiz metot çağırabildiği için adı taşır.
        private sealed class ComponentItem : DesignerActionMethodItem
        {
            private readonly ClassesActionList _owner;
            private readonly string _component;

            public ComponentItem(ClassesActionList owner, string component)
                : base(owner, nameof(Clear), component, "FormWind", $"Sets the classes to '{component}'.", true)
            {
                _owner = owner;
                _component = component;
            }

            public override void Invoke() => _owner.Apply(_component);
        }
    }
}
