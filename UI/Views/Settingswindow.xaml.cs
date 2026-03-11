using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Flux.Core;

namespace Flux.UI.Views
{
    public partial class SettingsWindow : Window
    {
        private static readonly string _appDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Flux");
        private static readonly string _settingsFile = Path.Combine(_appDir, "settings.json");
        private static readonly string _usedKeysFile = Path.Combine(_appDir, "used_keys.json");
        private static readonly string _licenseFile  = Path.Combine(_appDir, "license.key");

        public static AppSettings Current { get; private set; } = LoadSettings();

        private readonly SolidColorBrush _on  = new(Color.FromRgb(0xE8, 0xE8, 0xEE));
        private readonly SolidColorBrush _off = new(Color.FromRgb(0x60, 0x60, 0x68));
        private const string _placeholder = "FLUX-XXXX-XXXX-XXXX-XXXX";

        public SettingsWindow()
        {
            InitializeComponent();
            Localizer.SetLanguage(Current.Language);
            ApplyLocalization();
            RefreshUI();
        }

        // ── Localization ──────────────────────────────────────────────────────
        private void ApplyLocalization()
        {
            var L = Localizer.Get;
            TitleLabel.Text        = L("settings_title");
            LangSectionLabel.Text  = L("settings_language");
            ThemeSectionLabel.Text = L("settings_theme");
            KeySectionLabel.Text   = L("settings_key");
            AboutSectionLabel.Text = L("settings_about");
            KeyActivateLabel.Text  = L("settings_key_activate");
            VersionLabel.Text      = L("settings_version");
            if (KeyInput.Text == _placeholder || string.IsNullOrWhiteSpace(KeyInput.Text))
                KeyInput.Text = L("settings_key_placeholder");
        }

        // ── UI state ─────────────────────────────────────────────────────────
        private void RefreshUI()
        {
            // Language
            int col = Current.Language switch { "en" => 1, "fr" => 2, _ => 0 };
            Grid.SetColumn(LangIndicator, col);
            LblIT.Foreground = Current.Language == "it" ? _on : _off;
            LblEN.Foreground = Current.Language == "en" ? _on : _off;
            LblFR.Foreground = Current.Language == "fr" ? _on : _off;

            // Theme
            Grid.SetColumn(ThemeIndicator, Current.Theme == "light" ? 1 : 0);
            LblDark.Foreground  = Current.Theme == "dark"  ? _on : _off;
            LblLight.Foreground = Current.Theme == "light" ? _on : _off;

            // Premium status
            bool isAdmin = MainWindow.CurrentUser.Equals("Ema", StringComparison.OrdinalIgnoreCase);
            bool isPrem  = isAdmin || File.Exists(_licenseFile);

            PremiumStatus.Text = isAdmin ? Localizer.Get("settings_status_admin")
                               : isPrem  ? Localizer.Get("settings_status_premium")
                                         : Localizer.Get("settings_status_free");

            PremiumStatus.Foreground = isAdmin ? new SolidColorBrush(Color.FromRgb(0xF6, 0xC4, 0x5B))
                                     : isPrem  ? new SolidColorBrush(Color.FromRgb(0x9B, 0x7F, 0xF6))
                                               : _off;

            // Hide key section if already premium
            KeyActivateBtn.IsEnabled = !isPrem;
            if (isPrem && KeyStatusLabel.Text == "")
                KeyStatusLabel.Text = Localizer.Get("settings_key_ok");
        }

        // ── Language ──────────────────────────────────────────────────────────
        private void SetItaliano(object s, RoutedEventArgs e) => SetLang("it");
        private void SetEnglish(object s, RoutedEventArgs e)  => SetLang("en");
        private void SetFrancais(object s, RoutedEventArgs e) => SetLang("fr");

        private void SetLang(string lang)
        {
            Current.Language = lang;
            Localizer.SetLanguage(lang);
            Save();
            ApplyLocalization();
            RefreshUI();
            PropagateToMain();
        }

        // ── Theme ─────────────────────────────────────────────────────────────
        private void SetDark(object s, RoutedEventArgs e)  => SetTheme("dark");
        private void SetLight(object s, RoutedEventArgs e) => SetTheme("light");

        private void SetTheme(string theme)
        {
            Current.Theme = theme;
            Save();
            RefreshUI();
            foreach (Window w in Application.Current.Windows)
                if (w is MainWindow mw) mw.ApplyTheme(theme);
        }

        // ── License key activation ────────────────────────────────────────────
        private void ActivateKey(object s, RoutedEventArgs e)
        {
            string key = KeyInput.Text.Trim().ToUpperInvariant();
            if (key == _placeholder || string.IsNullOrEmpty(key)) return;

            // Normalise: accept with or without dashes
            if (!key.StartsWith("FLUX-"))
                key = "FLUX-" + key.Replace("-", "");

            // Check validity
            if (!ValidKeys.IsValid(key))
            {
                SetKeyStatus(Localizer.Get("settings_key_invalid"), false);
                return;
            }

            // Check if already used
            var used = LoadUsedKeys();
            string hash = Hash(key);
            if (used.Contains(hash))
            {
                SetKeyStatus(Localizer.Get("settings_key_used"), false);
                return;
            }

            // Mark as used
            used.Add(hash);
            SaveUsedKeys(used);

            // Write license
            Directory.CreateDirectory(_appDir);
            File.WriteAllText(_licenseFile, key);

            SetKeyStatus(Localizer.Get("settings_key_ok"), true);
            KeyInput.IsEnabled    = false;
            KeyActivateBtn.IsEnabled = false;

            // Refresh account badge in main window
            foreach (Window w in Application.Current.Windows)
                if (w is MainWindow mw)
                {
                    mw.RefreshAccount();
                    mw.TriggerPremiumUnlock();
                }
        }

        private void SetKeyStatus(string msg, bool success)
        {
            KeyStatusLabel.Text       = msg;
            KeyStatusLabel.Foreground = success
                ? new SolidColorBrush(Color.FromRgb(0x4F, 0xD9, 0x7A))
                : new SolidColorBrush(Color.FromRgb(0xF6, 0x6B, 0x5B));
            RefreshUI();
        }

        // ── Key input placeholder behaviour ───────────────────────────────────
        private void KeyInput_GotFocus(object s, RoutedEventArgs e)
        {
            if (KeyInput.Text == _placeholder || KeyInput.Text == Localizer.Get("settings_key_placeholder"))
            {
                KeyInput.Text = "";
                KeyInput.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xEE));
            }
        }

        private void KeyInput_LostFocus(object s, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(KeyInput.Text))
            {
                KeyInput.Text       = Localizer.Get("settings_key_placeholder");
                KeyInput.Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x48));
            }
        }

        // ── Propagate language to MainWindow ──────────────────────────────────
        private static void PropagateToMain()
        {
            foreach (Window w in Application.Current.Windows)
                if (w is MainWindow mw) mw.ApplyLocalization();
        }

        // ── Drag / Close ──────────────────────────────────────────────────────
        private void OnDrag(object s, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }
        private void CloseClick(object s, RoutedEventArgs e) => Close();

        // ── Persistence ───────────────────────────────────────────────────────
        public static AppSettings LoadSettings()
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Flux", "settings.json");
                if (File.Exists(path))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path))
                           ?? new AppSettings();
            }
            catch { }
            return new AppSettings();
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(_appDir);
                File.WriteAllText(_settingsFile, JsonSerializer.Serialize(Current));
            }
            catch { }
        }

        private static HashSet<string> LoadUsedKeys()
        {
            try
            {
                if (File.Exists(_usedKeysFile))
                    return JsonSerializer.Deserialize<HashSet<string>>(
                        File.ReadAllText(_usedKeysFile)) ?? new();
            }
            catch { }
            return new HashSet<string>();
        }

        private static void SaveUsedKeys(HashSet<string> used)
        {
            try
            {
                Directory.CreateDirectory(_appDir);
                File.WriteAllText(_usedKeysFile, JsonSerializer.Serialize(used));
            }
            catch { }
        }

        private static string Hash(string input) =>
            Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(input)));
    }

    public class AppSettings
    {
        public string Language { get; set; } = "it";
        public string Theme    { get; set; } = "dark";
    }
}
