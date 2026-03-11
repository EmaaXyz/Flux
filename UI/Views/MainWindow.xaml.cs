using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Flux.Core;

namespace Flux.UI.Views
{
    public partial class MainWindow : Window
    {
        public static string CurrentUser { get; set; } = "Utente";

        private static readonly string _appDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Flux");
        private static readonly string _licenseFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Flux", "license.key");

        private const string AdminUser = "Ema";

        public static bool IsUnlocked =>
            CurrentUser.Equals(AdminUser, StringComparison.OrdinalIgnoreCase)
            || File.Exists(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Flux", "license.key"));

        public MainWindow()
        {
            InitializeComponent();
            EnsureAdminLicense();
            RefreshAccount();
            ApplyLocalization();
            ApplyTheme(SettingsWindow.Current.Theme);
            ContentRendered += (_, _) => UnlockPremiumIfEligible();
        }

        private void OnTitleBarMouseDown(object s, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        // ── License ──────────────────────────────────────────────────────────
        private static void EnsureAdminLicense()
        {
            if (!CurrentUser.Equals(AdminUser, StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                Directory.CreateDirectory(_appDir);
                File.WriteAllText(Path.Combine(_appDir, "license.key"), "FLUX-ADMIN-FULL-ACCS");
            }
            catch { }
        }

        // ── Account ───────────────────────────────────────────────────────────
        public void RefreshAccount()
        {
            string user  = CurrentUser;
            bool isAdmin = user.Equals(AdminUser, StringComparison.OrdinalIgnoreCase);
            bool isPrem  = isAdmin || File.Exists(_licenseFile);

            UserNameLabel.Text = user;
            UserInitial.Text   = user.Length > 0 ? user[0].ToString().ToUpper() : "U";

            string plan = isAdmin ? Localizer.Get("plan_admin")
                        : isPrem  ? Localizer.Get("plan_premium")
                                  : Localizer.Get("plan_free");
            UserPlanLabel.Text = plan;

            if (isAdmin)
            {
                PlanBadgeText.Text       = "ADMIN";
                PlanBadgeText.Foreground = B(0xF6, 0xC4, 0x5B);
                PlanBadge.BorderBrush    = BA(0x60, 0xF6, 0xC4, 0x5B);
                PlanBadge.Background     = BA(0x25, 0xF6, 0xC4, 0x5B);
                UserInitial.Foreground   = B(0xF6, 0xC4, 0x5B);
            }
            else if (isPrem)
            {
                PlanBadgeText.Text       = "PREMIUM";
                PlanBadgeText.Foreground = B(0x9B, 0x7F, 0xF6);
                PlanBadge.BorderBrush    = BA(0x60, 0x9B, 0x7F, 0xF6);
                PlanBadge.Background     = BA(0x25, 0x9B, 0x7F, 0xF6);
                UserInitial.Foreground   = B(0x9B, 0x7F, 0xF6);
            }
            else
            {
                PlanBadgeText.Text       = "FREE";
                PlanBadgeText.Foreground = B(0x60, 0x60, 0x68);
                PlanBadge.BorderBrush    = BA(0x20, 0xFF, 0xFF, 0xFF);
                PlanBadge.Background     = BA(0x10, 0xFF, 0xFF, 0xFF);
                UserInitial.Foreground   = B(0x5B, 0x9C, 0xF6);
            }
        }

        // ── Localization ──────────────────────────────────────────────────────
        public void ApplyLocalization()
        {
            Localizer.SetLanguage(SettingsWindow.Current.Language);
            var L = Localizer.Get;

            // Sidebar labels
            NavTweaksLabel.Text  = L("nav_tweaks");
            NavMonitorLabel.Text = L("nav_monitor");

            // Find and update sidebar section labels by searching visual tree
            UpdateTextBlockByName(this, "SidebarOverviewLabel",  L("nav_overview"));
            UpdateTextBlockByName(this, "SidebarAccountLabel",   L("nav_account"));

            // Page title based on current page
            if (TweaksPage.Visibility == Visibility.Visible)
            {
                PageTitle.Text    = L("title_tweaks");
                PageSubtitle.Text = L("sub_tweaks");
            }
            else
            {
                PageTitle.Text    = L("title_monitor");
                PageSubtitle.Text = L("sub_monitor");
            }
        }

        private static void UpdateTextBlockByName(DependencyObject node, string name, string text)
        {
            if (node is FrameworkElement fe && fe.Name == name && fe is TextBlock tb)
            { tb.Text = text; return; }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                UpdateTextBlockByName(VisualTreeHelper.GetChild(node, i), name, text);
        }

        // ── Unlock ────────────────────────────────────────────────────────────
        public void TriggerPremiumUnlock() => UnlockPremiumIfEligible();

        private void UnlockPremiumIfEligible()
        {
            if (!IsUnlocked) return;
            WalkAndUnlock(this);
        }

        private static void WalkAndUnlock(DependencyObject node)
        {
            int n = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is Border b && Math.Abs(b.Opacity - 0.55) < 0.03)
                {
                    b.Opacity     = 1.0;
                    b.BorderBrush = new LinearGradientBrush(
                        Color.FromArgb(0x30, 0x4F, 0xD9, 0x7A),
                        Color.FromArgb(0x10, 0x4F, 0xD9, 0x7A), 90);
                    SwapBadgeForButton(b);
                }
                WalkAndUnlock(child);
            }
        }

        private static void SwapBadgeForButton(DependencyObject cardBorder)
        {
            Grid? cardGrid = FindDirectGrid(cardBorder);
            if (cardGrid == null) return;

            Border? badge    = null;
            TextBlock? lckTb = null;

            foreach (UIElement el in cardGrid.Children)
            {
                if (el is Border bd && ContainsText(bd, "* PREMIUM")) badge = bd;
                if (el is Border iconBd)
                {
                    var t = FindTb(iconBd, "LCK");
                    if (t != null) lckTb = t;
                }
            }

            if (lckTb != null)
            {
                lckTb.Text       = "OK";
                lckTb.Foreground = B(0x4F, 0xD9, 0x7A);
            }

            if (badge != null)
            {
                string name = GetTweakName(cardGrid);
                int col = Grid.GetColumn(badge);
                cardGrid.Children.Remove(badge);
                var btn = BuildExecBtn(name);
                Grid.SetColumn(btn, col);
                cardGrid.Children.Add(btn);
            }
        }

        private static Button BuildExecBtn(string tweakName)
        {
            var label = Localizer.Get("btn_execute");
            var btn = new Button
            {
                VerticalAlignment = VerticalAlignment.Center,
                Cursor    = Cursors.Hand,
                Padding   = new Thickness(12, 6, 12, 6),
                BorderThickness = new Thickness(1),
                Background  = new LinearGradientBrush(
                    Color.FromArgb(0x25, 0x4F, 0xD9, 0x7A),
                    Color.FromArgb(0x10, 0x4F, 0xD9, 0x7A), 90),
                BorderBrush = BA(0x50, 0x4F, 0xD9, 0x7A),
                Foreground  = B(0x4F, 0xD9, 0x7A),
            };

            var t = new ControlTemplate(typeof(Button));
            var f = new FrameworkElementFactory(typeof(Border));
            f.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            f.SetBinding(Border.PaddingProperty,    Bind("Padding"));
            f.SetBinding(Border.BackgroundProperty, Bind("Background"));
            f.SetBinding(Border.BorderBrushProperty,Bind("BorderBrush"));
            f.SetBinding(Border.BorderThicknessProperty, Bind("BorderThickness"));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty,   VerticalAlignment.Center);
            f.AppendChild(cp);
            t.VisualTree = f;
            btn.Template  = t;

            btn.Content = label;
            btn.Click  += (_, _) => ExecuteTweak(tweakName);
            return btn;
        }

        private static System.Windows.Data.Binding Bind(string path) =>
            new(path) { RelativeSource = new System.Windows.Data.RelativeSource(
                System.Windows.Data.RelativeSourceMode.TemplatedParent) };

        // ── Premium tweak logic ───────────────────────────────────────────────
        private static void ExecuteTweak(string name)
        {
            try
            {
                switch (name)
                {
                    case "DNS over HTTPS":
                        RunCmd("reg", @"add ""HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters"" /v EnableAutoDoh /t REG_DWORD /d 2 /f");
                        Msg("DNS over HTTPS abilitato."); break;
                    case "TCP/IP Optimizer":
                        RunCmd("netsh", "int tcp set global autotuninglevel=normal");
                        RunCmd("netsh", "int tcp set global chimney=enabled");
                        RunCmd("netsh", "int tcp set global dca=enabled");
                        Msg("TCP/IP ottimizzato."); break;
                    case "GPU Shader Cache Cleaner":
                        CleanDir(Path.Combine(Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData), @"NVIDIA\DXCache"));
                        Msg("Shader cache GPU rimossa."); break;
                    case "Disable Windows Telemetry":
                        RunCmd("sc", "stop DiagTrack");
                        RunCmd("sc", "config DiagTrack start= disabled");
                        RunCmd("reg", @"add ""HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection"" /v AllowTelemetry /t REG_DWORD /d 0 /f");
                        Msg("Telemetria disabilitata."); break;
                    case "MSI Mode (GPU / NIC)":
                        Msg("Usa MSI Utility v3 per abilitare MSI su GPU e NIC."); break;
                    case "HPET Disable":
                        RunCmd("bcdedit", "/deletevalue useplatformclock");
                        RunCmd("bcdedit", "/set disabledynamictick yes");
                        Msg("HPET disabilitato. Riavvia per applicare."); break;
                    case "Disable Xbox Game Bar":
                        RunCmd("reg", @"add ""HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR"" /v AppCaptureEnabled /t REG_DWORD /d 0 /f");
                        RunCmd("reg", @"add ""HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR"" /v AllowGameDVR /t REG_DWORD /d 0 /f");
                        Msg("Xbox Game Bar disabilitato."); break;
                    case "SSD Optimizer":
                        RunCmd("fsutil", "behavior set DisableDeleteNotify 0");
                        Msg("SSD ottimizzato: TRIM abilitato."); break;
                    case "Process Priority Scheduler":
                        RunCmd("reg", @"add ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\game.exe\PerfOptions"" /v CpuPriorityClass /t REG_DWORD /d 3 /f");
                        Msg("Process Priority impostato su High."); break;
                    case "Registry Cleaner Avanzato":
                        RunCmd("reg", @"delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs"" /f");
                        RunCmd("reg", @"delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU"" /f");
                        Msg("Registro pulito."); break;
                    case "Boot Time Optimizer":
                        RunCmd("reg", @"add ""HKLM\SYSTEM\CurrentControlSet\Control"" /v WaitToKillServiceTimeout /t REG_SZ /d 2000 /f");
                        RunCmd("bcdedit", "/timeout 5");
                        Msg("Boot ottimizzato."); break;
                    case "CPU Core Parking Disabler":
                        RunCmd("powercfg", "-setacvalueindex scheme_current sub_processor CPMINCORES 100");
                        RunCmd("powercfg", "-setactive scheme_current");
                        Msg("CPU Core Parking disabilitato."); break;
                    case "Network Adapter Optimizer":
                        RunCmd("netsh", "int tcp set global rss=enabled");
                        Msg("Adapter di rete ottimizzato."); break;
                    case "Windows Search Disabler":
                        RunCmd("sc", "stop WSearch");
                        RunCmd("sc", "config WSearch start= disabled");
                        Msg("Windows Search disabilitato."); break;
                    case "Virtual Memory Tuner":
                        Msg("Vai in Sistema > Impostazioni avanzate > Memoria virtuale.\nConsigliato: min 4096 MB, max = RAM."); break;
                    case "DirectX Shader Optimizer":
                        CleanDir(Path.Combine(Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData),
                            @"Microsoft\DirectX Shader Cache"));
                        Msg("DirectX Shader Cache ripulita."); break;
                    case "Audio Latency Reducer":
                        RunCmd("reg", @"add ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"" /v SystemResponsiveness /t REG_DWORD /d 0 /f");
                        Msg("Latenza audio ridotta."); break;
                    case "Auto-Tweak Scheduler":
                        Msg("Auto-Tweak Scheduler: funzionalita in arrivo nel prossimo aggiornamento."); break;
                    default:
                        Msg($"{name} eseguito."); break;
                }
            }
            catch (Exception ex)
            { MessageBox.Show($"Errore: {ex.Message}", "Flux", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private static void CleanDir(string path)
        {
            if (!Directory.Exists(path)) return;
            foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                try { File.Delete(f); } catch { }
        }

        // ── Theme ─────────────────────────────────────────────────────────────
        public void ApplyTheme(string theme)
        {
            RootBorder.Background    = theme == "light" ? B(0xF0, 0xF0, 0xF4) : B(0x0A, 0x0A, 0x0C);
            SidebarBorder.Background = theme == "light" ? B(0xE0, 0xE0, 0xE6) : B(0x0E, 0x0E, 0x11);
        }

        // ── Nav ───────────────────────────────────────────────────────────────
        private void NavToTweaks(object s, MouseButtonEventArgs e)
        {
            TweaksPage.Visibility  = Visibility.Visible;
            MonitorPage.Visibility = Visibility.Collapsed;
            PageTitle.Text    = Localizer.Get("title_tweaks");
            PageSubtitle.Text = Localizer.Get("sub_tweaks");
            SetNavActive(NavTweaksIndicator, NavTweaksLabel);
            SetNavInactive(NavMonitorIndicator, NavMonitorLabel);
        }

        private void NavToMonitor(object s, MouseButtonEventArgs e)
        {
            TweaksPage.Visibility  = Visibility.Collapsed;
            MonitorPage.Visibility = Visibility.Visible;
            PageTitle.Text    = Localizer.Get("title_monitor");
            PageSubtitle.Text = Localizer.Get("sub_monitor");
            SetNavActive(NavMonitorIndicator, NavMonitorLabel);
            SetNavInactive(NavTweaksIndicator, NavTweaksLabel);
        }

        private static void SetNavActive(Border b, TextBlock t)
        {
            b.Background  = BA(0x15, 0xFF, 0xFF, 0xFF);
            b.BorderBrush = BA(0x20, 0xFF, 0xFF, 0xFF);
            t.Foreground  = B(0xE8, 0xE8, 0xEE);
        }
        private static void SetNavInactive(Border b, TextBlock t)
        {
            b.Background  = new SolidColorBrush(Colors.Transparent);
            b.BorderBrush = new SolidColorBrush(Colors.Transparent);
            t.Foreground  = B(0x60, 0x60, 0x68);
        }

        // ── Account card click ────────────────────────────────────────────────
        private void OpenPremium(object s, MouseButtonEventArgs e)
        {
            if (IsUnlocked)
            {
                bool isAdmin = CurrentUser.Equals(AdminUser, StringComparison.OrdinalIgnoreCase);
                Msg($"Utente: {CurrentUser}\nPiano: {(isAdmin ? "Admin" : "Premium")}\nTweak sbloccati: tutti");
            }
            else new PremiumWindow().ShowDialog();
        }

        private void OpenPremiumBtn(object s, RoutedEventArgs e)
        { if (!IsUnlocked) new PremiumWindow().ShowDialog(); }

        // ── Settings / Logout ─────────────────────────────────────────────────
        private void OpenSettings(object s, RoutedEventArgs e)
        {
            new SettingsWindow().ShowDialog();
            ApplyLocalization();
            ApplyTheme(SettingsWindow.Current.Theme);
        }

        private void DoLogout(object s, RoutedEventArgs e)
        {
            try { File.Delete(Path.Combine(_appDir, "session.json")); } catch { }
            new LoginWindow().Show();
            Close();
        }

        // ── Free tweaks ───────────────────────────────────────────────────────
        private void ApplyHighPerformance(object s, RoutedEventArgs e)
        { RunCmd("powercfg", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"); Msg("Piano energetico: Alte Prestazioni."); }

        private void FlushRam(object s, RoutedEventArgs e)
        {
            System.GC.Collect(); System.GC.WaitForPendingFinalizers();
            foreach (var p in Process.GetProcesses()) try { EmptyWorkingSet(p.Handle); } catch { }
            Msg("RAM flush completato.");
        }
        [System.Runtime.InteropServices.DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(System.IntPtr hProcess);

        private void DisableVisualFx(object s, RoutedEventArgs e)
        { RunCmd("reg", @"add ""HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"" /v VisualFXSetting /t REG_DWORD /d 2 /f"); Msg("Effetti visivi disabilitati."); }

        private void CleanTemp(object s, RoutedEventArgs e)
        {
            int c = 0;
            foreach (var dir in new[] { Path.GetTempPath(), @"C:\Windows\Temp", @"C:\Windows\Prefetch" })
                try {
                    foreach (var f in Directory.GetFiles(dir))       try { File.Delete(f); c++; } catch { }
                    foreach (var d in Directory.GetDirectories(dir)) try { Directory.Delete(d, true); c++; } catch { }
                } catch { }
            Msg($"Pulizia: {c} elementi rimossi.");
        }

        private void DisableSysMain(object s, RoutedEventArgs e)
        { RunCmd("sc", "stop SysMain"); RunCmd("sc", "config SysMain start= disabled"); Msg("SysMain disabilitato."); }

        private void FlushDns(object s, RoutedEventArgs e)
        { RunCmd("ipconfig", "/flushdns"); Msg("Cache DNS svuotata."); }

        private void DisableHibernation(object s, RoutedEventArgs e)
        { RunCmd("powercfg", "/hibernate off"); Msg("Ibernazione disabilitata."); }

        private void MinimizeClick(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void CloseClick(object s, RoutedEventArgs e) => Close();

        // ── Helpers ───────────────────────────────────────────────────────────
        private static void Msg(string t) =>
            MessageBox.Show(t, "Flux", MessageBoxButton.OK, MessageBoxImage.Information);

        private static void RunCmd(string exe, string args)
        {
            using var p = Process.Start(new ProcessStartInfo(exe, args)
                { CreateNoWindow = true, UseShellExecute = false });
            p?.WaitForExit(3000);
        }

        private static Grid? FindDirectGrid(DependencyObject p)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(p); i++)
                if (VisualTreeHelper.GetChild(p, i) is Grid g) return g;
            return null;
        }

        private static bool ContainsText(DependencyObject n, string txt)
        {
            if (n is TextBlock tb && tb.Text == txt) return true;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(n); i++)
                if (ContainsText(VisualTreeHelper.GetChild(n, i), txt)) return true;
            return false;
        }

        private static TextBlock? FindTb(DependencyObject n, string txt)
        {
            if (n is TextBlock tb && tb.Text == txt) return tb;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(n); i++)
            {
                var r = FindTb(VisualTreeHelper.GetChild(n, i), txt);
                if (r != null) return r;
            }
            return null;
        }

        private static string GetTweakName(Grid g)
        {
            foreach (UIElement el in g.Children)
                if (el is StackPanel sp && Grid.GetColumn(sp) == 1)
                    foreach (var c in sp.Children)
                    {
                        if (c is TextBlock tb && tb.FontWeight == FontWeights.Bold) return tb.Text;
                        if (c is StackPanel inner)
                            foreach (var ic in inner.Children)
                                if (ic is TextBlock itb && itb.FontWeight == FontWeights.Bold)
                                    return itb.Text;
                    }
            return "Tweak";
        }

        private static SolidColorBrush B(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
        private static SolidColorBrush BA(byte a, byte r, byte g, byte b) => new(Color.FromArgb(a, r, g, b));

        protected override void OnClosed(System.EventArgs e)
        {
            (DataContext as System.IDisposable)?.Dispose();
            base.OnClosed(e);
        }
    }
}
