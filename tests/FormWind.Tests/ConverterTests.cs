#if !NETFRAMEWORK
using System.Collections.Generic;
using System.Drawing;
using FormWind.Convert;
using Xunit;

namespace FormWind.Tests;

// Araç Console.Out'a yazar; çıktıyı yakalayan testler aynı anda çalışmaz.
[Collection("Konsol çıktısı")]
public class ConverterTests
{
    private const string Designer = """
        namespace Demo
        {
            partial class OrderForm
            {
                private System.ComponentModel.IContainer components = null;

                private void InitializeComponent()
                {
                    this.btnSave = new System.Windows.Forms.Button();
                    this.lblTitle = new System.Windows.Forms.Label();
                    this.SuspendLayout();
                    //
                    // btnSave
                    //
                    this.btnSave.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(99)))), ((int)(((byte)(235)))));
                    this.btnSave.FlatAppearance.BorderSize = 0;
                    this.btnSave.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(29)))), ((int)(((byte)(78)))), ((int)(((byte)(216)))));
                    this.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
                    this.btnSave.Font = new System.Drawing.Font("Segoe UI Semibold", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
                    this.btnSave.ForeColor = System.Drawing.Color.White;
                    this.btnSave.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
                    this.btnSave.Text = "Kaydet";
                    //
                    // lblTitle
                    //
                    this.lblTitle.BackColor = System.Drawing.SystemColors.Control;
                    this.lblTitle.Font = new System.Drawing.Font("Georgia", 14F);
                    this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(140)))), ((int)(((byte)(60)))));
                    this.lblTitle.Text = "Siparişler";
                    //
                    // OrderForm
                    //
                    this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(251)))));
                    this.ResumeLayout(false);
                }

                private System.Windows.Forms.Button btnSave;
                private System.Windows.Forms.Label lblTitle;
            }
        }
        """;

    [Fact]
    public void Converts_colors_fonts_padding_and_flat_appearance()
    {
        var result = new DesignerConverter().Convert(Designer);
        var save = result.Controls[0];
        Assert.Equal("btnSave", save.Control);
        Assert.Equal(new[] { "bg-blue-600", "border-0", "hover:bg-blue-700", "text-sm", "font-semibold", "text-white", "px-3", "py-1.5" }, save.Classes);
        Assert.Empty(save.Notes);

        Assert.Contains("this.tailwindProvider1.SetClasses(this.btnSave, \"bg-blue-600 border-0 hover:bg-blue-700 text-sm font-semibold text-white px-3 py-1.5\");", result.Source);
        Assert.DoesNotContain("this.btnSave.BackColor", result.Source);
        Assert.Contains("this.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;", result.Source);   // davranış satırları kalır
        Assert.Contains("this.btnSave.Text = \"Kaydet\";", result.Source);
    }

    [Fact]
    public void Leaves_system_colors_and_other_font_families_with_notes()
    {
        var result = new DesignerConverter().Convert(Designer);
        var title = result.Controls[1];
        Assert.Equal(new[] { "text-[#5a8c3c]" }, title.Classes);                      // palette uzak: keyfi renk
        Assert.Contains(title.Notes, n => n.Contains("sistem rengi"));
        Assert.Contains(title.Notes, n => n.Contains("Georgia"));
        Assert.Contains("this.lblTitle.BackColor = System.Drawing.SystemColors.Control;", result.Source);
        Assert.Contains("this.lblTitle.Font = new System.Drawing.Font(\"Georgia\", 14F);", result.Source);

        var form = result.Controls[2];
        Assert.Equal("this", form.Control);
        Assert.Equal(new[] { "bg-gray-50" }, form.Classes);
        Assert.Contains("this.tailwindProvider1.SetClasses(this, \"bg-gray-50\");", result.Source);
    }

    [Fact]
    public void Adds_the_provider_once_and_merges_into_existing_calls()
    {
        var result = new DesignerConverter().Convert(Designer);
        Assert.True(result.ProviderAdded);
        Assert.Contains("this.components = new System.ComponentModel.Container();", result.Source);
        Assert.Contains("this.tailwindProvider1 = new FormWind.TailwindProvider(this.components);", result.Source);
        Assert.Contains("private FormWind.TailwindProvider tailwindProvider1;", result.Source);
        Assert.True(result.Source.IndexOf("new FormWind.TailwindProvider") < result.Source.IndexOf("SetClasses"));

        // İkinci çalıştırma: değişecek bir şey yok, sağlayıcı tekrar eklenmez.
        var again = new DesignerConverter().Convert(result.Source);
        Assert.False(again.ProviderAdded);
        Assert.False(again.Changed);

        var withExisting = result.Source.Replace("this.btnSave.Text = \"Kaydet\";",
            "this.btnSave.Text = \"Kaydet\";\n            this.btnSave.ForeColor = System.Drawing.Color.Black;");
        var merged = new DesignerConverter().Convert(withExisting);
        Assert.Contains("SetClasses(this.btnSave, \"bg-blue-600 border-0 hover:bg-blue-700 text-sm font-semibold text-white px-3 py-1.5 text-black\")", merged.Source);
    }

    [Theory]
    [InlineData(8, "2")]
    [InlineData(6, "1.5")]
    [InlineData(1, "px")]
    [InlineData(7, "[7px]")]
    [InlineData(0, "0")]
    public void Maps_pixels_to_spacing(int pixels, string expected) => Assert.Equal(expected, DesignerConverter.Spacing(pixels));

    [Fact]
    public void Nearby_colors_snap_to_the_palette_with_a_note()
    {
        var converter = new DesignerConverter();
        Assert.Equal("blue-600", converter.ColorClass(Color.FromArgb(37, 99, 235), out var exact));
        Assert.Null(exact);
        Assert.Equal("blue-600", converter.ColorClass(Color.FromArgb(40, 100, 230), out var near));
        Assert.NotNull(near);
        Assert.Equal("transparent", converter.ColorClass(Color.Transparent, out _));

        // Üretilen keyfi class'lar parser'da çalışır.
        var style = TwParser.Parse("text-[#5a8c3c] text-[13px] py-1.5", TwTheme.Current).Resolve(TwState.None);
        Assert.Equal(Color.FromArgb(90, 140, 60), style.ForeColor);
        Assert.Equal(13f, style.FontSizePx);
    }

    // Visual Studio'nun yeni Designer'ı "this." yazmaz; Git Extensions'ın 189 Designer dosyasının hiçbirinde yoktu.
    private const string ModernDesigner = """
        namespace Demo
        {
            partial class SettingsForm
            {
                private System.ComponentModel.IContainer components = null;

                private void InitializeComponent()
                {
                    components = new System.ComponentModel.Container();
                    DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
                    buttonOk = new Button();
                    menuFile = new ToolStripMenuItem();
                    statusList = new GitUI.UserControls.StatusList();
                    pushButton = new GitUI.ToolStripPushButton();
                    SuspendLayout();
                    dataGridViewCellStyle1.BackColor = Color.White;
                    buttonOk.BackColor = Color.White;
                    buttonOk.Padding = new Padding(8);
                    menuFile.ForeColor = Color.Red;
                    statusList.ForeColor = Color.Black;
                    pushButton.BackColor = Color.White;
                    BackColor = Color.White;
                    ResumeLayout(false);
                }

                private Button buttonOk;
                private ToolStripMenuItem menuFile;
                private GitUI.UserControls.StatusList statusList;
                private GitUI.ToolStripPushButton pushButton;
            }
        }
        """;

    [Fact]
    public void Converts_designer_code_without_this_prefix()
    {
        var result = new DesignerConverter().Convert(ModernDesigner);

        Assert.Contains("this.tailwindProvider1.SetClasses(buttonOk, \"bg-white p-2\");", result.Source);
        Assert.Contains("this.tailwindProvider1.SetClasses(this, \"bg-white\");", result.Source);
        Assert.DoesNotContain("buttonOk.BackColor", result.Source);
        Assert.Contains("this.tailwindProvider1 = new FormWind.TailwindProvider(this.components);", result.Source);
    }

    [Fact]
    public void Leaves_toolstrip_items_and_local_cell_styles_alone()
    {
        var result = new DesignerConverter().Convert(ModernDesigner);

        // Control olmayan öğeler SetClasses alamaz: satır kalır, not düşülür.
        Assert.Contains("menuFile.ForeColor = Color.Red;", result.Source);
        Assert.Contains("pushButton.BackColor = Color.White;", result.Source);
        Assert.DoesNotContain("SetClasses(menuFile", result.Source);
        Assert.DoesNotContain("SetClasses(pushButton", result.Source);
        Assert.Contains(result.Controls, c => c.Control == "menuFile" && c.Notes.Exists(n => n.Contains("Control değil")));
        // Yerel hücre stili hiç dokunulmaz.
        Assert.Contains("dataGridViewCellStyle1.BackColor = Color.White;", result.Source);
        Assert.DoesNotContain(result.Controls, c => c.Control == "dataGridViewCellStyle1");
        // Uygulamanın kendi kontrolü: notla dönüştürülür.
        Assert.Contains("SetClasses(statusList, \"text-black\")", result.Source);
        Assert.Contains(result.Controls, c => c.Control == "statusList" && c.Notes.Exists(n => n.Contains("doğrulanamadı")));
    }

    [Fact]
    public void Local_controls_are_referenced_without_this()
    {
        const string source = """
            partial class AboutForm
            {
                private System.ComponentModel.IContainer components = null;

                private void InitializeComponent()
                {
                    TableLayoutPanel tableLayoutPanel1;
                    components = new System.ComponentModel.Container();
                    tableLayoutPanel1 = new TableLayoutPanel();
                    tableLayoutPanel1.Padding = new Padding(8);
                }
            }
            """;
        var result = new DesignerConverter().Convert(source);

        Assert.Contains("this.tailwindProvider1.SetClasses(tableLayoutPanel1, \"p-2\");", result.Source);
    }

    [Fact]
    public void Existing_components_container_is_reused_not_recreated()
    {
        // Yeni Designer "components = new ..." yazar; ikinci bir kapsayıcı provider'ı dispose edilmeyen ilk kapsayıcıda bırakırdı.
        var result = new DesignerConverter().Convert(ModernDesigner);

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(result.Source, @"components\s*=\s*new"));
        var lines = result.Source.Split('\n');
        int container = System.Array.FindIndex(lines, l => l.Contains("components = new"));
        Assert.Contains("this.tailwindProvider1 = new FormWind.TailwindProvider(this.components);", lines[container + 1]);
    }

    [Fact]
    public void Adds_a_components_field_when_the_class_has_none_and_notes_disposal()
    {
        const string source = """
            namespace Demo;

            internal partial class FilterToolBar
            {
                private void InitializeComponent()
                {
                    filterBox = new ToolStripTextBox();
                    Padding = new Padding(0);
                }

                private ToolStripTextBox filterBox;
            }
            """;
        var result = new DesignerConverter().Convert(source);

        Assert.Contains("private System.ComponentModel.IContainer components = null;", result.Source);
        Assert.Contains("this.components = new System.ComponentModel.Container();", result.Source);
        Assert.Contains(result.FileNotes, n => n.Contains("components.Dispose()"));
    }

    [Fact]
    public void Provider_field_is_added_after_protected_internal_fields()
    {
        const string source = """
            namespace Demo;

            partial class GitExtensionsDialog
            {
                private System.ComponentModel.IContainer components = null;

                private void InitializeComponent()
                {
                    MainPanel = new Panel();
                    MainPanel.Padding = new Padding(12);
                }

                protected internal Panel MainPanel;
            }
            """;
        var result = new DesignerConverter().Convert(source);

        Assert.Contains("    protected internal Panel MainPanel;\n    private FormWind.TailwindProvider tailwindProvider1;", result.Source.Replace("\r\n", "\n"));
    }

    // Greenshot 1.3'te bulundu: eski Designer süslü parantezi metotla aynı satıra yazar ve sekmeyle girintiler.
    [Fact]
    public void Adds_the_provider_when_the_brace_is_on_the_method_line()
    {
        const string source = "namespace Demo {\n\tpartial class PleaseWaitForm {\n\t\tprivate System.ComponentModel.IContainer components = null;\n\n" +
                              "\t\tprivate void InitializeComponent() {\n\t\t\tthis.label1 = new System.Windows.Forms.Label();\n" +
                              "\t\t\tthis.label1.Padding = new System.Windows.Forms.Padding(10);\n\t\t}\n" +
                              "\t\tprivate System.Windows.Forms.Label label1;\n\t}\n}";
        var result = new DesignerConverter().Convert(source);

        Assert.Contains("\t\t\tthis.components = new System.ComponentModel.Container();", result.Source);
        Assert.Contains("\t\t\tthis.tailwindProvider1 = new FormWind.TailwindProvider(this.components);", result.Source);
        Assert.Contains("\t\tprivate FormWind.TailwindProvider tailwindProvider1;", result.Source);
    }

    // Greenshot 1.3'te bulundu: FontFamilyComboBox bir ToolStripComboBox'tır; adından anlaşılmaz, SetClasses derlenmezdi.
    [Fact]
    public void Resolves_application_types_through_their_base_types()
    {
        var baseTypes = DesignerConverter.CollectBaseTypes(new[]
        {
            "internal class FontFamilyComboBox : ToolStripComboBox, IGreenshotLanguageBindable { }",
            "public class GreenshotButton : Button, IGreenshotLanguageBindable { }",
            "public class FancyPanel : BasePanel { } public abstract class BasePanel : System.Windows.Forms.Panel { }",
        });

        Assert.False(DesignerConverter.IsControlType("Greenshot.Editor.Controls.FontFamilyComboBox", baseTypes, out var comboUnverified));
        Assert.False(comboUnverified);
        Assert.True(DesignerConverter.IsControlType("GreenshotButton", baseTypes, out var buttonUnverified));
        Assert.False(buttonUnverified);
        Assert.True(DesignerConverter.IsControlType("FancyPanel", baseTypes, out var chainUnverified));
        Assert.False(chainUnverified);
    }

    [Theory]
    [InlineData("System.Windows.Forms.Button", true, false)]
    [InlineData("ToolStripMenuItem", false, false)]
    [InlineData("System.Windows.Forms.DataGridViewTextBoxColumn", false, false)]
    [InlineData("GitUI.UserControls.RevisionGridControl", true, true)]
    [InlineData("GitUI.ToolStripPushButton", false, true)]
    public void Classifies_control_types(string type, bool control, bool unverified)
    {
        Assert.Equal(control, DesignerConverter.IsControlType(type, out var isUnverified));
        Assert.Equal(unverified, isUnverified);
    }

    private const string VbDesigner = """
        <Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
        Partial Class OrderForm
            Inherits System.Windows.Forms.Form

            'Form overrides dispose to clean up the component list.
            <System.Diagnostics.DebuggerNonUserCode()> _
            Protected Overrides Sub Dispose(ByVal disposing As Boolean)
                Try
                    If disposing AndAlso components IsNot Nothing Then
                        components.Dispose()
                    End If
                Finally
                    MyBase.Dispose(disposing)
                End Try
            End Sub

            Private components As System.ComponentModel.IContainer

            <System.Diagnostics.DebuggerStepThrough()> _
            Private Sub InitializeComponent()
                Me.btnSave = New System.Windows.Forms.Button()
                Me.lblTitle = New System.Windows.Forms.Label()
                Me.colName = New System.Windows.Forms.DataGridViewTextBoxColumn()
                Me.SuspendLayout()
                '
                'btnSave
                '
                Me.btnSave.BackColor = System.Drawing.Color.FromArgb(CType(CType(37, Byte), Integer), CType(CType(99, Byte), Integer), CType(CType(235, Byte), Integer))
                Me.btnSave.FlatAppearance.BorderSize = 0
                Me.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat
                Me.btnSave.Font = New System.Drawing.Font("Segoe UI Semibold", 10.5!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(162, Byte))
                Me.btnSave.ForeColor = System.Drawing.Color.White
                Me.btnSave.Padding = New System.Windows.Forms.Padding(12, 6, 12, 6)
                Me.btnSave.Text = "Kaydet"
                '
                'lblTitle
                '
                Me.lblTitle.BackColor = System.Drawing.SystemColors.Control
                Me.lblTitle.Font = New System.Drawing.Font("Segoe UI", 12.0!, CType((System.Drawing.FontStyle.Bold Or System.Drawing.FontStyle.Italic), System.Drawing.FontStyle))
                Me.lblTitle.Text = "Siparişler"
                '
                'colName
                '
                Me.colName.DefaultCellStyle.BackColor = System.Drawing.Color.White
                '
                'OrderForm
                '
                Me.BackColor = System.Drawing.Color.White
                Me.Controls.Add(Me.btnSave)
                Me.Controls.Add(Me.lblTitle)
                Me.ResumeLayout(False)
            End Sub

            Friend WithEvents btnSave As System.Windows.Forms.Button
            Friend WithEvents lblTitle As System.Windows.Forms.Label
            Friend WithEvents colName As System.Windows.Forms.DataGridViewTextBoxColumn
        End Class
        """;

    [Fact]
    public void Converts_visual_basic_designer_files()
    {
        var result = new DesignerConverter().Convert(VbDesigner);

        Assert.Equal("TailwindProvider1", result.ProviderName);
        Assert.True(result.ProviderAdded);
        var source = result.Source.Replace("\r\n", "\n");
        // SetClasses kontrolün son çevrilen satırından (Padding) sonra gelir.
        Assert.Contains("        Me.TailwindProvider1.SetClasses(Me.btnSave, \"bg-blue-600 border-0 text-sm font-semibold text-white px-3 py-1.5\")\n        Me.btnSave.Text = \"Kaydet\"", source);
        Assert.Contains("Me.TailwindProvider1.SetClasses(Me.lblTitle, \"text-base font-bold italic\")", result.Source);
        Assert.Contains("Me.TailwindProvider1.SetClasses(Me, \"bg-white\")", result.Source);
        Assert.DoesNotContain("Me.btnSave.ForeColor", result.Source);
        // Sistem rengi ve Control olmayan kolon bırakılır.
        Assert.Contains("Me.lblTitle.BackColor = System.Drawing.SystemColors.Control", result.Source);
        Assert.Contains("Me.colName.DefaultCellStyle.BackColor = System.Drawing.Color.White", result.Source);
        // VB sözdizimi: noktalı virgül yok; provider components'a eklenir, alanı WithEvents.
        Assert.DoesNotContain(";", result.Source);
        Assert.Contains("        Me.components = New System.ComponentModel.Container()", result.Source);
        Assert.Contains("        Me.TailwindProvider1 = New FormWind.TailwindProvider(Me.components)", result.Source);
        Assert.Contains("    Friend WithEvents colName As System.Windows.Forms.DataGridViewTextBoxColumn\n    Friend WithEvents TailwindProvider1 As FormWind.TailwindProvider", source);
        Assert.Empty(result.FileNotes); // components alanı ve Dispose zaten var
    }

    [Fact]
    public void Visual_basic_conversion_merges_into_existing_set_classes_and_keeps_unqualified_names()
    {
        const string source = """
            Partial Class SettingsForm
                Inherits System.Windows.Forms.Form

                Private Sub InitializeComponent()
                    Me.components = New System.ComponentModel.Container()
                    buttonOk = New Button()
                    Me.Tw = New FormWind.TailwindProvider(Me.components)
                    buttonOk.BackColor = Color.White
                    Me.Tw.SetClasses(buttonOk, "rounded-md")
                    BackColor = Color.Black
                End Sub

                Friend WithEvents buttonOk As Button
                Friend WithEvents Tw As FormWind.TailwindProvider
                Private components As System.ComponentModel.IContainer
            End Class
            """;

        var result = new DesignerConverter().Convert(source);

        Assert.False(result.ProviderAdded);
        Assert.Equal("Tw", result.ProviderName);
        Assert.Contains("Me.Tw.SetClasses(buttonOk, \"rounded-md bg-white\")", result.Source);
        Assert.Contains("Me.Tw.SetClasses(Me, \"bg-black\")", result.Source);
        Assert.DoesNotContain("buttonOk.BackColor", result.Source);
    }

    [Fact]
    public void Visual_basic_file_without_fields_gets_them_before_end_class()
    {
        const string source = """
            Partial Class EmptyForm
                Private Sub InitializeComponent()
                    Me.BackColor = System.Drawing.Color.White
                End Sub
            End Class
            """;

        var result = new DesignerConverter().Convert(source);
        var lines = result.Source.Replace("\r\n", "\n").Split('\n');

        int endClass = System.Array.FindIndex(lines, l => l.Trim() == "End Class");
        Assert.Equal("    Friend WithEvents TailwindProvider1 As FormWind.TailwindProvider", lines[endClass - 1]);
        Assert.Equal("    Private components As System.ComponentModel.IContainer", lines[endClass - 2]);
        Assert.Contains("Dispose", Assert.Single(result.FileNotes));
    }

    [Fact]
    public void Base_types_are_collected_from_visual_basic_sources()
    {
        var map = DesignerConverter.CollectBaseTypes(new[]
        {
            "Public Class FontComboBox\r\n    Inherits ToolStripComboBox\r\nEnd Class",
            "Friend NotInheritable Class Badge : Inherits System.Windows.Forms.Label\nEnd Class",
            "Public Class Repository(Of T)\n    Inherits Base(Of T)\nEnd Class",
        });

        Assert.Equal("ToolStripComboBox", map["FontComboBox"]);
        Assert.Equal("System.Windows.Forms.Label", map["Badge"]);
        Assert.True(DesignerConverter.IsControlType("FontComboBox", map, out var unverified) == false && !unverified);
    }

    // KeePass 2.61.1 denemesi: yalnızca notu olan (çevrilmeyen) kontrol rapora boş bir class kodu (``) olarak yazılıyordu;
    // ikinci --write çalıştırması hiçbir dosyayı değiştirmediği halde "Dosyalar güncellendi." diyordu. Araç ikinci kez
    // çalıştırılınca dosya değişmemeli.
    [Fact]
    public void Tool_reports_unconverted_controls_and_a_second_run_changes_nothing()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "formwind-convert-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        var output = System.Console.Out;
        try
        {
            var file = System.IO.Path.Combine(folder, "EditForm.Designer.cs");
            System.IO.File.WriteAllText(file, Designer.Replace("\"Georgia\", 14F", "\"Courier New\", 9F"), new System.Text.UTF8Encoding(true));
            var report = System.IO.Path.Combine(folder, "rapor.md");

            using var first = new System.IO.StringWriter();
            System.Console.SetOut(first);
            Assert.Equal(0, FormWind.Convert.Program.Main(new[] { folder, "--write", "--report", report }));
            var converted = System.IO.File.ReadAllBytes(file);
            var markdown = System.IO.File.ReadAllText(report);
            Assert.Contains("- **lblTitle**: `", markdown);                                     // rengi çevrildi
            Assert.DoesNotContain("``", markdown);

            using var second = new System.IO.StringWriter();
            System.Console.SetOut(second);
            Assert.Equal(0, FormWind.Convert.Program.Main(new[] { folder, "--write" }));
            Assert.Equal(converted, System.IO.File.ReadAllBytes(file));                          // ikinci çalıştırma değiştirmez
            Assert.Contains("Değiştirilecek bir şey yok.", second.ToString());
            Assert.DoesNotContain("Dosyalar güncellendi", second.ToString());
        }
        finally
        {
            System.Console.SetOut(output);
            try { System.IO.Directory.Delete(folder, true); } catch (System.IO.IOException) { }
        }
    }

    [Fact]
    public void Report_marks_controls_that_only_have_notes()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "formwind-convert-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        var output = System.Console.Out;
        try
        {
            // Yalnızca farklı bir font ailesi: satır kalır, kontrol için class yok, not var.
            const string onlyFont = """
                namespace Demo
                {
                    partial class KeyForm
                    {
                        private void InitializeComponent()
                        {
                            this.m_rbKeySeq = new System.Windows.Forms.RadioButton();
                            this.m_rbKeySeq.Font = new System.Drawing.Font("Courier New", 8.25F);
                            this.m_rbKeySeq.Text = "Sıra";
                        }

                        private System.Windows.Forms.RadioButton m_rbKeySeq;
                    }
                }
                """;
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "KeyForm.Designer.cs"), onlyFont);
            var report = System.IO.Path.Combine(folder, "rapor.md");
            using var writer = new System.IO.StringWriter();
            System.Console.SetOut(writer);
            Assert.Equal(0, FormWind.Convert.Program.Main(new[] { folder, "--report", report }));
            var markdown = System.IO.File.ReadAllText(report);
            Assert.Contains("- **m_rbKeySeq**: (çevrilen yok)", markdown);
            Assert.DoesNotContain("``", markdown);
        }
        finally
        {
            System.Console.SetOut(output);
            try { System.IO.Directory.Delete(folder, true); } catch (System.IO.IOException) { }
        }
    }
}
#endif
