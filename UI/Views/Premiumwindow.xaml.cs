using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Flux.UI.Views
{
    public partial class PremiumWindow : Window
    {
        // Semplice licenza locale — in produzione usa un server di validazione
        private static readonly string _licenseFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Flux", "license.key");

        // Paypal.me link — sostituisci con il tuo
        private const string PayPalUrl = "https://paypal.me/EMAA104";

        public static bool IsPremiumActive =>
            File.Exists(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Flux", "license.key"));

        public PremiumWindow() => InitializeComponent();

        private void OnDrag(object s, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }

        private void CloseClick(object s, RoutedEventArgs e) => Close();

        private void PayClick(object s, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(PayPalUrl) { UseShellExecute = true });
        }

        private void ActivateClick(object s, RoutedEventArgs e)
        {
            string key = LicenseBox.Text.Trim();
            if (ValidateKey(key))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_licenseFile)!);
                File.WriteAllText(_licenseFile, key);
                MessageBox.Show("Premium attivato! Riavvia Flux.", "Flux Premium",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            else
            {
                LicenseMsg.Text = "Chiave non valida";
                LicenseMsg.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }

        private static bool ValidateKey(string key)
        {
            // Formato chiave demo: FLUX-XXXX-XXXX-XXXX (16 chars dopo i prefissi)
            // In produzione: valida contro un server REST
            if (string.IsNullOrEmpty(key)) return false;
            if (!key.StartsWith("FLUX-", StringComparison.OrdinalIgnoreCase)) return false;
            var parts = key.Split('-');
            return parts.Length == 4 && parts[1].Length == 4
                                     && parts[2].Length == 4
                                     && parts[3].Length == 4;
        }
    }
}
