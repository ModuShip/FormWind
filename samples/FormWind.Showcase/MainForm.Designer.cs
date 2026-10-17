namespace FormWind.Showcase
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tailwindProvider1 = new FormWind.TailwindProvider(this.components);
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblSubtitle = new System.Windows.Forms.Label();
            this.chkDark = new FormWind.TwSwitch();
            this.pnlCard = new FormWind.TwPanel();
            this.pnlCardHeader = new FormWind.TwPanel();
            this.icoCustomer = new FormWind.TwIcon();
            this.lblCardTitle = new System.Windows.Forms.Label();
            this.lblName = new System.Windows.Forms.Label();
            this.txtName = new FormWind.TwTextBox();
            this.lblEmail = new System.Windows.Forms.Label();
            this.txtEmail = new FormWind.TwTextBox();
            this.chkActive = new FormWind.TwCheckBox();
            this.pnlSpacer = new FormWind.TwPanel();
            this.pnlActions = new FormWind.TwPanel();
            this.btnCancel = new FormWind.TwButton();
            this.btnSave = new FormWind.TwButton();
            this.pnlSide = new FormWind.TwPanel();
            this.lblSideTitle = new System.Windows.Forms.Label();
            this.btnStandard = new System.Windows.Forms.Button();
            this.btnDanger = new FormWind.TwButton();
            this.btnDisabled = new FormWind.TwButton();
            this.lblLink = new System.Windows.Forms.Label();
            this.lblBadge = new System.Windows.Forms.Label();
            this.pnlTable = new FormWind.TwPanel();
            this.lblTableTitle = new System.Windows.Forms.Label();
            this.gridOrders = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.tailwindProvider1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridOrders)).BeginInit();
            this.pnlTable.SuspendLayout();
            this.pnlCard.SuspendLayout();
            this.pnlCardHeader.SuspendLayout();
            this.pnlActions.SuspendLayout();
            this.pnlSide.SuspendLayout();
            this.SuspendLayout();
            //
            // lblTitle
            //
            this.lblTitle.AutoSize = true;
            this.lblTitle.Location = new System.Drawing.Point(24, 20);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "FormWind vitrini";
            this.tailwindProvider1.SetClasses(this.lblTitle, "text-2xl font-semibold text-gray-900 dark:text-white");
            //
            // lblSubtitle
            //
            this.lblSubtitle.AutoSize = true;
            this.lblSubtitle.Location = new System.Drawing.Point(26, 60);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.TabIndex = 1;
            this.lblSubtitle.Text = "Tailwind benzeri class\'larla stillendirilmiş ve flex ile yerleştirilmiş WinForms kontrolleri";
            this.tailwindProvider1.SetClasses(this.lblSubtitle, "text-sm text-gray-500 dark:text-gray-400");
            //
            // chkDark
            //
            this.chkDark.AutoSize = true;
            this.chkDark.Location = new System.Drawing.Point(620, 26);
            this.chkDark.Name = "chkDark";
            this.chkDark.TabIndex = 2;
            this.chkDark.Text = "Koyu tema";
            this.chkDark.CheckedChanged += new System.EventHandler(this.chkDark_CheckedChanged);
            this.tailwindProvider1.SetClasses(this.chkDark, "text-sm text-gray-700 dark:text-gray-200 cursor-pointer accent-blue-600 dark:fill-gray-600");
            //
            // pnlCard: çocuklarını flex-col yerleştirir; konumları motor belirler.
            //
            this.pnlCard.Controls.Add(this.pnlCardHeader);
            this.pnlCard.Controls.Add(this.lblName);
            this.pnlCard.Controls.Add(this.txtName);
            this.pnlCard.Controls.Add(this.lblEmail);
            this.pnlCard.Controls.Add(this.txtEmail);
            this.pnlCard.Controls.Add(this.chkActive);
            this.pnlCard.Controls.Add(this.pnlSpacer);
            this.pnlCard.Controls.Add(this.pnlActions);
            this.pnlCard.Location = new System.Drawing.Point(24, 96);
            this.pnlCard.Name = "pnlCard";
            this.pnlCard.Size = new System.Drawing.Size(400, 330);
            this.pnlCard.TabIndex = 3;
            this.tailwindProvider1.SetClasses(this.pnlCard, "card flex flex-col gap-1");
            //
            // pnlCardHeader: ikon + başlık; içeriğine göre boyutlanır (AutoSize).
            //
            this.pnlCardHeader.AutoSize = true;
            this.pnlCardHeader.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlCardHeader.Controls.Add(this.icoCustomer);
            this.pnlCardHeader.Controls.Add(this.lblCardTitle);
            this.pnlCardHeader.Name = "pnlCardHeader";
            this.pnlCardHeader.TabIndex = 0;
            this.tailwindProvider1.SetClasses(this.pnlCardHeader, "flex items-center gap-2 mb-3");
            //
            // icoCustomer
            //
            this.icoCustomer.IconName = "user";
            this.icoCustomer.Name = "icoCustomer";
            this.icoCustomer.TabIndex = 0;
            this.tailwindProvider1.SetClasses(this.icoCustomer, "text-xl text-blue-600 dark:text-blue-400");
            //
            // lblCardTitle
            //
            this.lblCardTitle.AutoSize = true;
            this.lblCardTitle.Name = "lblCardTitle";
            this.lblCardTitle.TabIndex = 0;
            this.lblCardTitle.Text = "Müşteri bilgileri";
            this.tailwindProvider1.SetClasses(this.lblCardTitle, "text-lg font-semibold text-gray-900 dark:text-white");
            //
            // lblName
            //
            this.lblName.AutoSize = true;
            this.lblName.Name = "lblName";
            this.lblName.TabIndex = 1;
            this.lblName.Text = "Ad soyad";
            this.tailwindProvider1.SetClasses(this.lblName, "text-sm text-gray-600 dark:text-gray-300");
            //
            // txtName
            //
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(368, 36);
            this.txtName.TabIndex = 2;
            this.txtName.Text = "Ayşe Yılmaz";
            this.tailwindProvider1.SetClasses(this.txtName, "input mb-3");
            //
            // lblEmail
            //
            this.lblEmail.AutoSize = true;
            this.lblEmail.Name = "lblEmail";
            this.lblEmail.TabIndex = 3;
            this.lblEmail.Text = "E-posta";
            this.tailwindProvider1.SetClasses(this.lblEmail, "text-sm text-gray-600 dark:text-gray-300");
            //
            // txtEmail
            //
            this.txtEmail.Name = "txtEmail";
            this.txtEmail.Size = new System.Drawing.Size(368, 36);
            this.txtEmail.TabIndex = 4;
            this.txtEmail.PlaceholderText = "ornek@firma.com";
            this.tailwindProvider1.SetClasses(this.txtEmail, "input mb-3");
            //
            // chkActive
            //
            this.chkActive.AutoSize = true;
            this.chkActive.Checked = true;
            this.chkActive.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkActive.Name = "chkActive";
            this.chkActive.TabIndex = 5;
            this.chkActive.Text = "Aktif müşteri";
            this.tailwindProvider1.SetClasses(this.chkActive, "text-sm text-gray-700 dark:text-gray-200 accent-blue-600 dark:fill-gray-900 dark:border-gray-600");
            //
            // pnlSpacer: kalan dikey boşluğu kaplar, butonları kartın altına iter.
            //
            this.pnlSpacer.Name = "pnlSpacer";
            this.pnlSpacer.Size = new System.Drawing.Size(10, 0);
            this.pnlSpacer.TabIndex = 6;
            this.tailwindProvider1.SetClasses(this.pnlSpacer, "flex-1");
            //
            // pnlActions
            //
            this.pnlActions.Controls.Add(this.btnCancel);
            this.pnlActions.Controls.Add(this.btnSave);
            this.pnlActions.Name = "pnlActions";
            this.pnlActions.Size = new System.Drawing.Size(368, 40);
            this.pnlActions.TabIndex = 7;
            this.tailwindProvider1.SetClasses(this.pnlActions, "flex justify-end gap-2");
            //
            // btnCancel
            //
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(104, 40);
            this.btnCancel.TabIndex = 0;
            this.btnCancel.Text = "İptal";
            this.tailwindProvider1.SetClasses(this.btnCancel, "btn-ghost");
            //
            // btnSave
            //
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(104, 40);
            this.btnSave.TabIndex = 1;
            this.btnSave.Text = "Kaydet";
            this.btnSave.IconName = "save";
            this.tailwindProvider1.SetClasses(this.btnSave, "btn-primary");
            //
            // pnlSide
            //
            this.pnlSide.Controls.Add(this.lblSideTitle);
            this.pnlSide.Controls.Add(this.btnStandard);
            this.pnlSide.Controls.Add(this.btnDanger);
            this.pnlSide.Controls.Add(this.btnDisabled);
            this.pnlSide.Controls.Add(this.lblLink);
            this.pnlSide.Controls.Add(this.lblBadge);
            this.pnlSide.Location = new System.Drawing.Point(448, 96);
            this.pnlSide.Name = "pnlSide";
            this.pnlSide.Size = new System.Drawing.Size(288, 330);
            this.pnlSide.TabIndex = 4;
            this.tailwindProvider1.SetClasses(this.pnlSide, "card shadow-lg flex flex-col gap-3");
            //
            // lblSideTitle
            //
            this.lblSideTitle.AutoSize = true;
            this.lblSideTitle.Name = "lblSideTitle";
            this.lblSideTitle.TabIndex = 0;
            this.lblSideTitle.Text = "Butonlar";
            this.tailwindProvider1.SetClasses(this.lblSideTitle, "text-lg font-semibold text-gray-900 dark:text-white");
            //
            // btnStandard
            //
            this.btnStandard.Name = "btnStandard";
            this.btnStandard.Size = new System.Drawing.Size(256, 40);
            this.btnStandard.TabIndex = 1;
            this.btnStandard.Text = "Standart Button";
            this.tailwindProvider1.SetClasses(this.btnStandard, "btn-primary");
            //
            // btnDanger
            //
            this.btnDanger.Name = "btnDanger";
            this.btnDanger.Size = new System.Drawing.Size(256, 40);
            this.btnDanger.TabIndex = 2;
            this.btnDanger.Text = "Sil";
            this.btnDanger.IconName = "trash";
            this.tailwindProvider1.SetClasses(this.btnDanger, "btn-danger");
            //
            // btnDisabled
            //
            this.btnDisabled.Enabled = false;
            this.btnDisabled.Name = "btnDisabled";
            this.btnDisabled.Size = new System.Drawing.Size(256, 40);
            this.btnDisabled.TabIndex = 3;
            this.btnDisabled.Text = "Devre dışı";
            this.tailwindProvider1.SetClasses(this.btnDisabled, "btn-primary");
            //
            // lblLink
            //
            this.lblLink.AutoSize = true;
            this.lblLink.Name = "lblLink";
            this.lblLink.TabIndex = 4;
            this.lblLink.Text = "Bağlantı stili (üzerine gel)";
            this.tailwindProvider1.SetClasses(this.lblLink, "text-sm text-blue-600 hover:text-blue-800 underline cursor-pointer dark:text-blue-400 dark:hover:text-blue-300");
            //
            // lblBadge
            //
            this.lblBadge.Name = "lblBadge";
            this.lblBadge.Size = new System.Drawing.Size(72, 24);
            this.lblBadge.TabIndex = 5;
            this.lblBadge.Text = "Yeni";
            this.lblBadge.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.tailwindProvider1.SetClasses(this.lblBadge, "badge self-start");
            //
            // pnlTable
            //
            this.pnlTable.Controls.Add(this.lblTableTitle);
            this.pnlTable.Controls.Add(this.gridOrders);
            this.pnlTable.Location = new System.Drawing.Point(24, 446);
            this.pnlTable.Name = "pnlTable";
            this.pnlTable.Size = new System.Drawing.Size(712, 262);
            this.pnlTable.TabIndex = 5;
            this.tailwindProvider1.SetClasses(this.pnlTable, "card flex flex-col gap-3");
            //
            // lblTableTitle
            //
            this.lblTableTitle.AutoSize = true;
            this.lblTableTitle.Name = "lblTableTitle";
            this.lblTableTitle.TabIndex = 0;
            this.lblTableTitle.Text = "Son siparişler";
            this.tailwindProvider1.SetClasses(this.lblTableTitle, "text-lg font-semibold text-gray-900 dark:text-white");
            //
            // gridOrders
            //
            this.gridOrders.AllowUserToAddRows = false;
            this.gridOrders.AllowUserToDeleteRows = false;
            this.gridOrders.AllowUserToResizeRows = false;
            this.gridOrders.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridOrders.Name = "gridOrders";
            this.gridOrders.ReadOnly = true;
            this.gridOrders.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridOrders.Size = new System.Drawing.Size(680, 190);
            this.gridOrders.TabIndex = 1;
            this.tailwindProvider1.SetClasses(this.gridOrders, "table flex-1");
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(760, 732);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblSubtitle);
            this.Controls.Add(this.chkDark);
            this.Controls.Add(this.pnlCard);
            this.Controls.Add(this.pnlSide);
            this.Controls.Add(this.pnlTable);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "FormWind Showcase";
            this.tailwindProvider1.SetClasses(this, "bg-gray-50 dark:bg-gray-900");
            ((System.ComponentModel.ISupportInitialize)(this.tailwindProvider1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridOrders)).EndInit();
            this.pnlTable.ResumeLayout(false);
            this.pnlTable.PerformLayout();
            this.pnlCardHeader.ResumeLayout(false);
            this.pnlCardHeader.PerformLayout();
            this.pnlCard.ResumeLayout(false);
            this.pnlCard.PerformLayout();
            this.pnlActions.ResumeLayout(false);
            this.pnlSide.ResumeLayout(false);
            this.pnlSide.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private FormWind.TailwindProvider tailwindProvider1;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSubtitle;
        private FormWind.TwSwitch chkDark;
        private FormWind.TwPanel pnlCard;
        private FormWind.TwPanel pnlCardHeader;
        private FormWind.TwIcon icoCustomer;
        private System.Windows.Forms.Label lblCardTitle;
        private System.Windows.Forms.Label lblName;
        private FormWind.TwTextBox txtName;
        private System.Windows.Forms.Label lblEmail;
        private FormWind.TwTextBox txtEmail;
        private FormWind.TwCheckBox chkActive;
        private FormWind.TwPanel pnlSpacer;
        private FormWind.TwPanel pnlActions;
        private FormWind.TwButton btnCancel;
        private FormWind.TwButton btnSave;
        private FormWind.TwPanel pnlSide;
        private System.Windows.Forms.Label lblSideTitle;
        private System.Windows.Forms.Button btnStandard;
        private FormWind.TwButton btnDanger;
        private FormWind.TwButton btnDisabled;
        private System.Windows.Forms.Label lblLink;
        private System.Windows.Forms.Label lblBadge;
        private FormWind.TwPanel pnlTable;
        private System.Windows.Forms.Label lblTableTitle;
        private System.Windows.Forms.DataGridView gridOrders;
    }
}
