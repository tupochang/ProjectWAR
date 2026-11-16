using System;
using System.Drawing;
using System.Windows.Forms;

namespace Launcher
{
    public partial class ApocLauncher
    {
        private static readonly Color Surface = Color.FromArgb(24, 23, 22);
        private static readonly Color Field = Color.FromArgb(41, 38, 35);
        private static readonly Color Border = Color.FromArgb(92, 77, 58);
        private static readonly Color TextColor = Color.FromArgb(246, 238, 224);
        private static readonly Color Muted = Color.FromArgb(192, 181, 163);
        private static readonly Color Gold = Color.FromArgb(219, 171, 98);
        private static readonly Color LinkHover = Color.FromArgb(255, 215, 157);

        private System.ComponentModel.IContainer components;
        private Panel signInPanel;
        private Panel registerPanel;
        private ComboBox serverPicker;
        private Label serverAddress;
        private Label pageTitle;
        private Label status;
        private Label downloadStatus;
        private Label version;
        private TextBox signInName;
        private TextBox signInPassword;
        private TextBox registerName;
        private TextBox registerEmail;
        private TextBox registerPassword;
        private TextBox confirmPassword;
        private Button signIn;
        private Button register;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                components?.Dispose();

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            SuspendLayout();

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Surface;
            ClientSize = new Size(1024, 650);
            Font = new Font("Microsoft JhengHei UI", 10F);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)new System.ComponentModel.ComponentResourceManager(typeof(ApocLauncher)).GetObject("$this.Icon");
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ProjectWAR";
            Load += Form1_Load;
            FormClosed += Disconnect;

            var artwork = new Panel { Dock = DockStyle.Left, Width = 564, BackColor = Color.Black };
            artwork.Paint += PaintArtwork;
            Controls.Add(artwork);

            var account = new Panel { Location = new Point(564, 0), Size = new Size(460, 650) };
            Controls.Add(account);

            var wordmark = AddLabel(account, "ProjectWAR", 48, 34, 364, 31, Gold);
            wordmark.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
            pageTitle = AddLabel(account, "登入帳號", 46, 72, 366, 54, TextColor);
            pageTitle.Font = new Font(Font.FontFamily, 27F, FontStyle.Bold);

            AddLabel(account, "伺服器", 48, 140, 364, 21, Muted);
            serverAddress = AddLabel(account, "", 48, 168, 364, 32, TextColor);
            serverAddress.BackColor = Field;
            serverAddress.Padding = new Padding(10, 0, 0, 0);
            serverPicker = new ComboBox
            {
                AccessibleName = "伺服器",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(48, 168),
                Width = 364,
                TabIndex = 0,
                Visible = false
            };
            account.Controls.Add(serverPicker);

            signInPanel = new Panel { Location = new Point(48, 220), Size = new Size(364, 336), TabIndex = 1 };
            account.Controls.Add(signInPanel);
            signInName = AddInput(signInPanel, "帳號名稱", 0, 0, 64);
            signInPassword = AddInput(signInPanel, "密碼", 1, 72, 128, true);
            signIn = AddButton(signInPanel, "登入", 153, 2, SignIn_Click);
            AddLabel(signInPanel, "還沒有帳號？", 0, 215, 115, 32, Muted);
            AddLink(signInPanel, "建立帳號", 115, 215, 3, ShowRegister_Click);

            registerPanel = new Panel
            {
                Location = signInPanel.Location,
                Size = signInPanel.Size,
                TabIndex = 1,
                Visible = false
            };
            account.Controls.Add(registerPanel);
            registerName = AddInput(registerPanel, "帳號名稱", 0, 0, 64);
            registerEmail = AddInput(registerPanel, "電子郵件", 1, 61, 254);
            registerPassword = AddInput(registerPanel, "密碼", 2, 122, 128, true);
            confirmPassword = AddInput(registerPanel, "確認密碼", 3, 183, 128, true);
            register = AddButton(registerPanel, "建立帳號", 249, 4, Register_Click);
            AddLabel(registerPanel, "已有帳號？", 0, 298, 95, 32, Muted);
            AddLink(registerPanel, "返回登入", 95, 298, 5, ShowSignIn_Click);

            status = AddLabel(account, "", 48, 556, 364, 46, Gold);
            var footerLine = new Panel { Location = new Point(48, 613), Size = new Size(364, 1), BackColor = Border };
            account.Controls.Add(footerLine);
            version = AddLabel(account, "", 48, 620, 86, 23, Muted);
            downloadStatus = AddLabel(account, "", 134, 620, 278, 23, Muted);
            downloadStatus.TextAlign = ContentAlignment.MiddleRight;

            var patchTimer = new Timer(components) { Enabled = true, Interval = 250 };
            patchTimer.Tick += UpdatePatchStatus;
            AcceptButton = signIn;
            ResumeLayout(false);
        }

        private static void PaintArtwork(object sender, PaintEventArgs e)
        {
            var artwork = (Panel)sender;
            Image image = Properties.Resources.M55rLv3;
            float ratio = Math.Max((float)artwork.Width / image.Width, (float)artwork.Height / image.Height);
            int width = (int)Math.Ceiling(image.Width * ratio);
            int height = (int)Math.Ceiling(image.Height * ratio);
            e.Graphics.DrawImage(image, (artwork.Width - width) / 2, (artwork.Height - height) / 2, width, height);
        }

        private static Label AddLabel(Control parent, string text, int x, int y, int width, int height, Color color)
        {
            var label = new Label
            {
                Text = text,
                Bounds = new Rectangle(x, y, width, height),
                ForeColor = color,
                TextAlign = ContentAlignment.MiddleLeft
            };
            parent.Controls.Add(label);
            return label;
        }

        private static TextBox AddInput(Control parent, string name, int tabIndex, int y, int maxLength, bool password = false)
        {
            AddLabel(parent, name, 0, y, 364, 22, Muted).TabIndex = tabIndex * 2;
            var inputSurface = new Panel
            {
                Bounds = new Rectangle(0, y + 25, 364, 33),
                BackColor = Field,
                BorderStyle = BorderStyle.FixedSingle,
                TabIndex = tabIndex * 2 + 1
            };
            parent.Controls.Add(inputSurface);

            var input = new TextBox
            {
                AccessibleName = name,
                BackColor = Field,
                BorderStyle = BorderStyle.None,
                ForeColor = TextColor,
                Location = new Point(10, 6),
                MaxLength = maxLength,
                UseSystemPasswordChar = password,
                Width = 342
            };
            inputSurface.Controls.Add(input);
            inputSurface.Click += (sender, args) => input.Focus();
            return input;
        }

        private static Button AddButton(Control parent, string text, int y, int tabIndex, EventHandler click)
        {
            var button = new Button
            {
                BackColor = Gold,
                Bounds = new Rectangle(0, y, 364, 42),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(parent.Font.FontFamily, 11F, FontStyle.Bold),
                ForeColor = Surface,
                TabIndex = tabIndex * 2,
                Text = text,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = LinkHover;
            button.Click += click;
            parent.Controls.Add(button);
            return button;
        }

        private static void AddLink(Control parent, string text, int x, int y, int tabIndex, EventHandler click)
        {
            var link = new LinkLabel
            {
                ActiveLinkColor = LinkHover,
                Bounds = new Rectangle(x, y, 140, 32),
                LinkColor = Gold,
                TabIndex = tabIndex * 2,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                VisitedLinkColor = Gold
            };
            link.Click += click;
            parent.Controls.Add(link);
        }
    }
}
