#if !NETFRAMEWORK
using System;
using System.IO;
using System.Linq;
using FormWind.Convert;
using Xunit;

namespace FormWind.Tests;

// KeePass 2.61.1 entegrasyonunda elle düzeltilmesi gereken yerler ancak uygulama çalıştırılıp ekran görüntülerine bakılarak
// bulunabildi: grup ağacının kodda atanan rengi, giriş listesi öğe renkleri, RTF ile yüklenen giriş ayrıntıları, şifre tekrar
// kutusunun hata rengi, görseli kodda verilen sabit boyutlu "Tools" butonu. --audit bunları kod çalıştırmadan listeler.
// Araç Console.Out'a yazar; çıktıyı yakalayan testler aynı anda çalışmaz.
[Collection("Konsol çıktısı")]
public class MigrationAuditTests
{
    private const string MainDesigner = """
        namespace KeePass.Forms
        {
            partial class MainForm
            {
                private void InitializeComponent()
                {
                    this.m_tvGroups = new KeePass.UI.CustomTreeViewEx();
                    this.m_tvGroups.BackColor = System.Drawing.SystemColors.Window;
                    this.m_btnTools = new System.Windows.Forms.Button();
                    this.m_btnTools.Size = new System.Drawing.Size(80, 23);
                    this.m_btnTools.Text = "T&ools";
                    this.m_btnTools.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
                    this.m_btnIcon = new System.Windows.Forms.Button();
                    this.m_btnIcon.Size = new System.Drawing.Size(32, 23);
                    this.m_btnIcon.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
                }

                private KeePass.UI.CustomTreeViewEx m_tvGroups;
                private System.Windows.Forms.Button m_btnTools;
                private System.Windows.Forms.Button m_btnIcon;
                private System.Windows.Forms.ListView m_lvEntries;
            }
        }
        """;

    private const string MainCode = """
        using System.Drawing;
        using System.Windows.Forms;
        namespace KeePass.Forms
        {
            public partial class MainForm : Form
            {
                private void UpdateUIState(bool open)
                {
                    if (!open) m_tvGroups.BackColor = AppDefs.ColorControlDisabled;
                    else m_tvGroups.BackColor = SystemColors.Window;
                }

                private ListViewItem SetListEntry(PwEntry pe)
                {
                    ListViewItem lvi = new ListViewItem();
                    lvi.ForeColor = m_lvEntries.ForeColor;
                    var node = new TreeNode();
                    node.BackColor = Color.Empty;
                    return lvi;
                }

                private void OnFormLoad(object sender, System.EventArgs e)
                {
                    UIUtil.SetButtonImage(m_btnTools, Properties.Resources.Tools, true);
                    UIUtil.SetButtonImage(m_btnIcon, Properties.Resources.Key, true);
                    UIUtil.SetExplorerTheme(m_lvEntries.Handle);
                    m_lvEntries.OwnerDraw = true;
                    m_lvEntries.DrawItem += OnDrawItem;
                }

                private void Build(RichTextBox rtb, string strRtf, DataGridView dg)
                {
                    rtb.Rtf = strRtf;
                    dg.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 128);
                }
            }

            public static class UIUtil
            {
                public static void SetExplorerTheme(System.IntPtr hWnd) => NativeMethods.SetWindowTheme(hWnd, "explorer", null);
            }
        }
        """;

    // Aynı adlı buton başka bir formda yalnızca görselle: rapora girmez, MainForm'unkiyle karışmaz.
    private const string OtherForm = """
        namespace KeePass.Forms
        {
            partial class KeyPromptForm
            {
                private void InitializeComponent()
                {
                    this.m_btnTools = new System.Windows.Forms.Button();
                    this.m_btnTools.Image = global::KeePass.Properties.Resources.Folder;
                    this.m_btnTools.Size = new System.Drawing.Size(32, 23);
                    this.m_btnTools.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
                }

                private System.Windows.Forms.Button m_btnTools;
            }
        }
        """;

    private static readonly (string, string)[] Sources =
    {
        ("Forms/MainForm.Designer.cs", MainDesigner),
        ("Forms/MainForm.cs", MainCode),
        ("Forms/KeyPromptForm.Designer.cs", OtherForm),
    };

    [Fact]
    public void Finds_what_the_KeePass_integration_needed_by_hand()
    {
        var findings = MigrationAudit.Audit(Sources);
        string[] At(AuditCategory category) => findings.Where(f => f.Category == category).Select(f => $"{f.File}:{f.Line} {f.Code}").ToArray();

        // Designer'ın InitializeComponent'i dönüştürücünün işidir; taranmaz.
        Assert.Equal(new[]
        {
            "Forms/MainForm.cs:9 m_tvGroups.BackColor = AppDefs.ColorControlDisabled",
            "Forms/MainForm.cs:10 m_tvGroups.BackColor = SystemColors.Window",
        }, At(AuditCategory.RuntimeColor));
        Assert.Contains("ResetBackColor()", findings.First(f => f.Line == 10).Advice);
        Assert.Equal(new[] { "Forms/MainForm.cs:16 lvi.ForeColor = m_lvEntries.ForeColor", "Forms/MainForm.cs:18 node.BackColor = Color.Empty" }, At(AuditCategory.ItemColor));
        Assert.Equal(new[] { "Forms/MainForm.cs:34 dg.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 128)" }, At(AuditCategory.GridCellStyle));
        Assert.Equal(new[] { "Forms/MainForm.cs:33 rtb.Rtf = strRtf" }, At(AuditCategory.Rtf));
        Assert.Equal(new[]
        {
            "Forms/MainForm.cs:26 UIUtil.SetExplorerTheme(m_lvEntries.Handle)",                          // SetWindowTheme'i saran yöntem
            "Forms/MainForm.cs:40 NativeMethods.SetWindowTheme(hWnd, \"explorer\", null)",
        }, At(AuditCategory.WindowTheme));
        Assert.Equal(new[] { "Forms/MainForm.cs:27 m_lvEntries.OwnerDraw = true", "Forms/MainForm.cs:28 m_lvEntries.DrawItem += OnDrawItem" }, At(AuditCategory.OwnerDraw));
        // Görseli kodda verilen, yazılı "Tools"; yazısız m_btnIcon ve öteki formun aynı adlı butonu yok.
        Assert.Equal(new[] { "Forms/MainForm.Designer.cs:10 m_btnTools.Size = new System.Drawing.Size(80, 23)" }, At(AuditCategory.ImageButton));
    }

    [Fact]
    public void Tool_writes_the_report_and_changes_no_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), "formwind-audit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "Forms"));
        var output = Console.Out;
        try
        {
            foreach (var (file, source) in Sources) File.WriteAllText(Path.Combine(folder, file), source);
            var before = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllBytes).ToArray();
            var report = Path.Combine(Path.GetTempPath(), "formwind-audit-" + Guid.NewGuid().ToString("N") + ".md");

            using var writer = new StringWriter();
            Console.SetOut(writer);
            Assert.Equal(0, FormWind.Convert.Program.Main(new[] { folder, "--audit", "--report", report }));
            var markdown = File.ReadAllText(report);
            File.Delete(report);

            Assert.Contains("# FormWind geçiş öncesi tarama", markdown);
            Assert.Contains("| Kodda atanan renkler | 2 |", markdown);
            Assert.Contains("`Forms/MainForm.cs:9`", markdown);
            Assert.Contains("Kodda atanan renkler: 2", writer.ToString());
            Assert.Equal(before, Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllBytes).ToArray());
        }
        finally
        {
            Console.SetOut(output);
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }
}
#endif
