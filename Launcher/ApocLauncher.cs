using NLog;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Launcher
{
    public partial class ApocLauncher : Form
    {
        public bool LaunchLocalServer { get; }
        public bool AllowMYPPatch { get; }
        public bool AllowServerPatch { get; }
        public bool AllowWarClientLaunch { get; }
        public static ApocLauncher Acc;

        public static string LocalServerIP = "127.0.0.1";
        public static string TestServerIP = "127.0.0.1";
        public static int LocalServerPort = 8000;
        public static int TestServerPort = 8000;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private Patcher patcher;

        public ApocLauncher()
        {
            AllowWarClientLaunch = SafeReadAppSettings("AutoLaunch", true);
            AllowMYPPatch = SafeReadAppSettings("PatchMYP", true);
            AllowServerPatch = SafeReadAppSettings("PatchExe", true);
            LaunchLocalServer = SafeReadAppSettings("LaunchLocal", false);

            InitializeComponent();
            Acc = this;

            bool hasSeparateLocalServer = LaunchLocalServer &&
                (LocalServerIP != TestServerIP || LocalServerPort != TestServerPort);
            string defaultServer = FormatServer(TestServerIP, TestServerPort);
            if (hasSeparateLocalServer)
            {
                serverPicker.Items.Add(defaultServer);
                serverPicker.Items.Add(FormatServer(LocalServerIP, LocalServerPort));
                serverPicker.SelectedIndex = 0;
                serverPicker.Visible = true;
                serverAddress.Visible = false;
            }
            else
            {
                serverAddress.Text = defaultServer;
            }
        }

        private static string FormatServer(string address, int port)
        {
            bool isLocal = address.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                (IPAddress.TryParse(address, out IPAddress ip) && IPAddress.IsLoopback(ip));
            string name = isLocal ? "本機伺服器" : "伺服器";
            return $"{name}  ·  {address}:{port}";
        }

        private static bool SafeReadAppSettings(string key, bool defaultValue)
        {
            return bool.TryParse(ConfigurationManager.AppSettings[key], out bool value) ? value : defaultValue;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            version.Text = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
            signInName.Text = ConfigurationManager.AppSettings["LastUserCode"];

            if (AllowMYPPatch)
            {
                string endpoint = $"{ConfigurationManager.AppSettings["ServerPatchIPAddress"]}:{ConfigurationManager.AppSettings["ServerPatchPort"]}";
                string patchDirectory = ConfigurationManager.AppSettings["PatchDirectory"];
                _logger.Debug($"Calling Patcher Server on {endpoint}");
                patcher = new Patcher(_logger, endpoint);
                UpdatePatchStatus(this, EventArgs.Empty);
                var thread = new Thread(() => patcher.Patch(patchDirectory).Wait()) { IsBackground = true };
                thread.Start();
            }

            (string.IsNullOrEmpty(signInName.Text) ? signInName : signInPassword).Select();
        }

        private void Disconnect(object sender, FormClosedEventArgs e)
        {
            Client.Close();
        }

        public static string ConvertSHA256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] data = sha.ComputeHash(Encoding.Default.GetBytes(value));
                var hash = new StringBuilder(data.Length * 2);
                foreach (byte item in data)
                    hash.Append(item.ToString("x2"));

                return hash.ToString();
            }
        }

        public void Print(string message)
        {
        }

        private void SignIn_Click(object sender, EventArgs e)
        {
            string username = signInName.Text.Trim().ToLower();
            if (username.Length == 0)
            {
                ShowFieldError(signInName, "請輸入帳號名稱。");
                return;
            }

            if (signInPassword.Text.Length == 0)
            {
                ShowFieldError(signInPassword, "請輸入密碼。");
                return;
            }

            Client.User = username;
            var packet = new PacketOut((byte)Opcodes.CL_START);
            packet.WriteString(username);
            packet.WriteString(ConvertSHA256(username + ":" + signInPassword.Text.ToLower()));
            if (!ConnectForRequest("正在登入…", packet))
                return;

            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                KeyValueConfigurationElement lastUser = config.AppSettings.Settings["LastUserCode"];
                if (lastUser == null)
                    config.AppSettings.Settings.Add("LastUserCode", username);
                else
                    lastUser.Value = username;

                config.Save();
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch (ConfigurationErrorsException ex)
            {
                _logger.Warn(ex, "Could not save the last account name.");
            }
        }

        private void Register_Click(object sender, EventArgs e)
        {
            string username = registerName.Text.Trim().ToLower();
            string email = registerEmail.Text.Trim();
            string password = registerPassword.Text;
            if (username.Length == 0 || !IsAscii(username))
            {
                ShowFieldError(registerName, "請用半形英文、數字或符號輸入帳號名稱。");
                return;
            }

            if (!IsAscii(email) || !IsEmailAddress(email))
            {
                ShowFieldError(registerEmail, "請輸入有效的電子郵件地址。");
                return;
            }

            if (password.Length == 0 || !IsAscii(password))
            {
                ShowFieldError(registerPassword, "請用半形英文、數字或符號輸入密碼。");
                return;
            }

            if (password != confirmPassword.Text)
            {
                ShowFieldError(confirmPassword, "兩次輸入的密碼不一致。");
                return;
            }

            Client.User = username;
            var packet = new PacketOut((byte)Opcodes.CL_CREATE);
            packet.WriteString(username);
            packet.WriteString(password.ToLower());
            packet.WriteString(email);
            packet.WriteByte(0); // Default registration email language.
            ConnectForRequest("正在建立帳號…", packet);
        }

        private bool ConnectForRequest(string message, PacketOut request)
        {
            bool useLocal = serverPicker.SelectedIndex == 1;
            string address = useLocal ? LocalServerIP : TestServerIP;
            int port = useLocal ? LocalServerPort : TestServerPort;
            sendUI(message);
            if (Client.Connect(address, port, request))
                return true;

            request.Dispose();
            sendUI($"無法連線至 {address}:{port}。請確認 LauncherServer 已啟動。");
            return false;
        }

        private static bool IsAscii(string value)
        {
            // LauncherServer's packet reader does not decode UTF-8.
            foreach (char character in value)
            {
                if (character < 32 || character > 126)
                    return false;
            }
            return true;
        }

        private static bool IsEmailAddress(string value)
        {
            try
            {
                var address = new MailAddress(value);
                return address.Address == value && address.Host.Contains(".");
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private void ShowFieldError(TextBox field, string message)
        {
            sendUI(message);
            field.Focus();
        }

        private void ShowRegister_Click(object sender, EventArgs e)
        {
            registerName.Text = signInName.Text;
            ShowAccountPage(true);
        }

        private void ShowSignIn_Click(object sender, EventArgs e)
        {
            ShowAccountPage(false);
        }

        private void ShowAccountPage(bool showRegister)
        {
            signInPanel.Visible = !showRegister;
            registerPanel.Visible = showRegister;
            pageTitle.Text = showRegister ? "建立帳號" : "登入帳號";
            AcceptButton = showRegister ? register : signIn;
            signInPassword.Clear();
            registerPassword.Clear();
            confirmPassword.Clear();
            sendUI("");
            (showRegister ? registerName : signInName).Select();
        }

        internal void AccountCreated(string username)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AccountCreated(username)));
                return;
            }

            ShowAccountPage(false);
            signInName.Text = username;
            registerEmail.Clear();
            string address = serverPicker.SelectedIndex == 1 ? LocalServerIP : TestServerIP;
            sendUI(IPAddress.TryParse(address, out IPAddress ip) && IPAddress.IsLoopback(ip)
                ? "帳號已建立，請輸入密碼登入。"
                : "帳號已建立。請查看電子郵件，按伺服器指示完成驗證。");
            signInPassword.Select();
        }

        public void sendUI(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => sendUI(message)));
                return;
            }

            status.Text = message;
        }

        private void UpdatePatchStatus(object sender, EventArgs e)
        {
            if (patcher == null)
                return;

            Patcher.State state = patcher.CurrentState;
            signIn.Enabled = state == Patcher.State.Done || state == Patcher.State.Error;
            switch (state)
            {
                case Patcher.State.RequestManifest:
                    downloadStatus.Text = "正在檢查更新…";
                    break;
                case Patcher.State.ProcessManifest:
                    downloadStatus.Text = "正在準備更新…";
                    break;
                case Patcher.State.Downloading:
                    long percent = patcher.TotalDownloadSize > 0
                        ? patcher.Downloaded * 100 / patcher.TotalDownloadSize : 0;
                    downloadStatus.Text = $"正在下載更新  {percent}%";
                    break;
                default:
                    downloadStatus.Text = "";
                    break;
            }
        }
    }
}
