using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flux.UI.Views
{
    public partial class LoginWindow : Window
    {
        private static readonly string _dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Flux");
        private static readonly string _usersFile   = Path.Combine(_dataDir, "users.json");
        private static readonly string _sessionFile = Path.Combine(_dataDir, "session.json");

        private Dictionary<string, UserEntry> _users = new();

        private const string AdminUser = "Ema";
        private const string AdminPass = "24692469";

        public LoginWindow()
        {
            InitializeComponent();
            LoadUsers();
            if (TryAutoLogin()) return;
        }

        private bool TryAutoLogin()
        {
            try
            {
                if (!File.Exists(_sessionFile)) return false;
                var session = JsonSerializer.Deserialize<SessionData>(File.ReadAllText(_sessionFile));
                if (session is null || string.IsNullOrEmpty(session.Username)) return false;
                bool isAdmin = session.Username.Equals(AdminUser, StringComparison.OrdinalIgnoreCase);
                if (isAdmin || _users.ContainsKey(session.Username))
                {
                    OpenMain(session.Username, isAdmin);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private void OnDrag(object s, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }

        private void CloseClick(object s, RoutedEventArgs e) => Application.Current.Shutdown();

        private void ShowLogin(object s, RoutedEventArgs e)
        {
            LoginPanel.Visibility    = Visibility.Visible;
            RegisterPanel.Visibility = Visibility.Collapsed;
            Grid.SetColumn(TabIndicator, 0);
            BtnLogin.Foreground    = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xEE));
            BtnRegister.Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x68));
            Height = 600;
        }

        private void ShowRegister(object s, RoutedEventArgs e)
        {
            LoginPanel.Visibility    = Visibility.Collapsed;
            RegisterPanel.Visibility = Visibility.Visible;
            Grid.SetColumn(TabIndicator, 1);
            BtnRegister.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xEE));
            BtnLogin.Foreground    = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x68));
            Height = 680;
        }

        private void DoLogin(object s, RoutedEventArgs e)
        {
            LoginError.Text = "";
            string user = LoginUser.Text.Trim();
            string pass = LoginPass.Password;
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            { LoginError.Text = "Inserisci tutti i campi"; return; }

            bool isAdmin = user.Equals(AdminUser, StringComparison.OrdinalIgnoreCase) && pass == AdminPass;
            bool isUser  = !isAdmin && _users.TryGetValue(user.ToLower(), out var entry) && entry.PassHash == Hash(pass);

            if (!isAdmin && !isUser)
            { LoginError.Text = "Credenziali non valide"; return; }

            if (RememberMe.IsChecked == true) SaveSession(user);
            OpenMain(user, isAdmin);
        }

        private void DoRegister(object s, RoutedEventArgs e)
        {
            RegError.Text = "";
            string user    = RegUser.Text.Trim();
            string pass    = RegPass.Password;
            string confirm = RegConfirm.Password;

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            { RegError.Text = "Inserisci tutti i campi"; return; }
            if (user.Equals(AdminUser, StringComparison.OrdinalIgnoreCase))
            { RegError.Text = "Username non disponibile"; return; }
            if (pass != confirm)
            { RegError.Text = "Le password non coincidono"; return; }
            if (pass.Length < 6)
            { RegError.Text = "Password minimo 6 caratteri"; return; }
            if (_users.ContainsKey(user.ToLower()))
            { RegError.Text = "Username gia in uso"; return; }

            _users[user.ToLower()] = new UserEntry { PassHash = Hash(pass), IsPremium = false };
            SaveUsers();
            if (RememberMeReg.IsChecked == true) SaveSession(user);
            OpenMain(user, false);
        }

        private void OpenMain(string username, bool isAdmin)
        {
            if (isAdmin)
            {
                Directory.CreateDirectory(_dataDir);
                File.WriteAllText(Path.Combine(_dataDir, "license.key"), "FLUX-ADMIN-FULL-ACCS");
            }
            MainWindow.CurrentUser = username;
            new MainWindow().Show();
            Close();
        }

        private void SaveSession(string username)
        {
            try
            {
                Directory.CreateDirectory(_dataDir);
                File.WriteAllText(_sessionFile,
                    JsonSerializer.Serialize(new SessionData { Username = username }));
            }
            catch { }
        }

        private static string Hash(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return Convert.ToHexString(
                sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input)));
        }

        private void LoadUsers()
        {
            try
            {
                if (File.Exists(_usersFile))
                    _users = JsonSerializer.Deserialize<Dictionary<string, UserEntry>>(
                        File.ReadAllText(_usersFile)) ?? new();
            }
            catch { _users = new(); }
        }

        private void SaveUsers()
        {
            try
            {
                Directory.CreateDirectory(_dataDir);
                File.WriteAllText(_usersFile, JsonSerializer.Serialize(_users));
            }
            catch { }
        }
    }

    public class UserEntry
    {
        public string PassHash  { get; set; } = "";
        public bool   IsPremium { get; set; }
    }

    public class SessionData
    {
        public string Username { get; set; } = "";
    }
}