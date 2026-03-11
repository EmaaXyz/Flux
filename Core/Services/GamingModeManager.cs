using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Flux.Core.Models;

namespace Flux.Core.Services
{
    /// <summary>
    /// Manages Gaming Mode lifecycle:
    ///   1. Saves current power plan GUID
    ///   2. Switches to High Performance / Ultimate Performance
    ///   3. Terminates non-essential background processes
    ///   4. On deactivation, restores everything
    /// Requires the app to run as Administrator for process termination.
    /// </summary>
    public sealed class GamingModeManager
    {
        // â”€â”€ Power Plan GUIDs (built-in Windows plans) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private const string GUID_HIGH_PERFORMANCE       = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        private const string GUID_ULTIMATE_PERFORMANCE   = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        private const string GUID_BALANCED               = "381b4222-f694-41f0-9685-ff5bb260df2e";

        // â”€â”€ Non-killable: OS-critical and security processes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static readonly HashSet<string> _essentialProcesses = new(
            StringComparer.OrdinalIgnoreCase)
        {
            // Windows core
            "System", "smss", "csrss", "wininit", "winlogon", "services",
            "lsass", "lsm", "svchost", "dwm", "explorer", "conhost",
            "fontdrvhost", "sihost", "taskhostw", "ctfmon", "RuntimeBroker",
            "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost",
            "SearchIndexer", "spoolsv", "WUDFHost",
            // Security
            "MsMpEng", "NisSrv", "SecurityHealthService", "SgrmBroker",
            // GPU drivers
            "nvcontainer", "nvdisplay.container", "RtkAudUService64",
            // Flux itself
            "Flux"
        };

        // â”€â”€ Soft kill-list: safe non-gaming background apps â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static readonly HashSet<string> _softKillTargets = new(
            StringComparer.OrdinalIgnoreCase)
        {
            "OneDrive", "Teams", "Slack", "Discord",
            "Spotify", "chrome", "msedge", "firefox",
            "outlook", "WINWORD", "EXCEL", "POWERPNT",
            "SkypeApp", "YourPhone", "XboxApp", "GameBarPresenceWriter",
            "EpicGamesLauncher",          // launcher, not game
            "steam",                      // Steam client UI (not games)
            "AdobeUpdateService",
            "acrotray",                   // Adobe Acrobat tray
            "jusched",                    // Java updater
            "iTunesHelper",
            "dropbox"
        };

        // â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private GamingModeSession? _activeSession;
        public  GamingModeState    State => _activeSession?.State ?? GamingModeState.Off;

        public event EventHandler<GamingModeState>? StateChanged;
        public event Action<string>? LogMessage;

        // â”€â”€ Activate â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task ActivateAsync()
        {
            if (State != GamingModeState.Off) return;

            _activeSession = new GamingModeSession
            {
                StartedAt = DateTime.Now,
                State     = GamingModeState.Activating
            };
            RaiseState(GamingModeState.Activating);

            // 1 â€” Save & switch power plan
            _activeSession.PreviousPowerPlan = await GetActivePowerPlanGuidAsync();
            bool switched = await SetPowerPlanAsync(GUID_ULTIMATE_PERFORMANCE)
                         || await SetPowerPlanAsync(GUID_HIGH_PERFORMANCE);

            Log(switched
                ? "âœ“ Power plan â†’ High/Ultimate Performance"
                : "âš  Could not switch power plan (run as Admin)");

            // 2 â€” Kill soft targets
            await Task.Run(() => KillNonEssentialProcesses());

            // 3 â€” Raise priority of current foreground process
            BoostForegroundProcessPriority();

            _activeSession.State = GamingModeState.Active;
            RaiseState(GamingModeState.Active);
            Log($"âœ“ Gaming Mode active â€” {_activeSession.KilledProcessPids.Count} processes terminated");
        }

        // â”€â”€ Deactivate â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task DeactivateAsync()
        {
            if (_activeSession is null || State == GamingModeState.Off) return;

            RaiseState(GamingModeState.Deactivating);

            // Restore power plan
            if (!string.IsNullOrEmpty(_activeSession.PreviousPowerPlan))
            {
                await SetPowerPlanAsync(_activeSession.PreviousPowerPlan);
                Log($"âœ“ Power plan restored â†’ {_activeSession.PreviousPowerPlan}");
            }

            _activeSession = null;
            RaiseState(GamingModeState.Off);
            Log("âœ“ Gaming Mode deactivated");
        }

        // â”€â”€ Process Termination â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void KillNonEssentialProcesses()
        {
            var candidates = Process.GetProcesses()
                .Where(p => _softKillTargets.Contains(p.ProcessName)
                         && !_essentialProcesses.Contains(p.ProcessName))
                .ToList();

            foreach (var proc in candidates)
            {
                try
                {
                    int pid = proc.Id;
                    proc.Kill(entireProcessTree: true);
                    _activeSession!.KilledProcessPids.Add(pid);
                    Log($"  âœ• Killed: {proc.ProcessName} (PID {pid})");
                }
                catch (Exception ex)
                {
                    Log($"  âš  Could not kill {proc.ProcessName}: {ex.Message}");
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }

        public List<ProcessInfo> GetKillPreview()
        {
            return Process.GetProcesses()
                .Where(p => _softKillTargets.Contains(p.ProcessName))
                .Select(p =>
                {
                    long memMb = 0;
                    try { memMb = p.WorkingSet64 / (1024 * 1024); } catch { }
                    return new ProcessInfo
                    {
                        Pid       = p.Id,
                        Name      = p.ProcessName,
                        MemoryMb  = memMb,
                        IsEssential = false
                    };
                })
                .ToList();
        }

        // â”€â”€ Power Plan Helpers (powercfg.exe) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static async Task<bool> SetPowerPlanAsync(string guid)
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg.exe", $"/setactive {guid}")
                {
                    CreateNoWindow  = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var proc = Process.Start(psi);
                if (proc is null) return false;
                await proc.WaitForExitAsync();
                return proc.ExitCode == 0;
            }
            catch { return false; }
        }

        private static async Task<string> GetActivePowerPlanGuidAsync()
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg.exe", "/getactivescheme")
                {
                    CreateNoWindow         = true,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true
                };
                using var proc = Process.Start(psi)!;
                string output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                // Output: "Power Scheme GUID: xxxxxxxx-xxxx-..."
                var match = System.Text.RegularExpressions.Regex.Match(
                    output, @"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})");
                return match.Success ? match.Value : GUID_BALANCED;
            }
            catch { return GUID_BALANCED; }
        }

        private static void BoostForegroundProcessPriority()
        {
            try
            {
                // Set Flux itself to AboveNormal so polling stays responsive
                Process.GetCurrentProcess().PriorityClass =
                    ProcessPriorityClass.AboveNormal;
            }
            catch { /* no admin rights */ }
        }

        // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void RaiseState(GamingModeState s) =>
            StateChanged?.Invoke(this, s);

        private void Log(string msg) =>
            LogMessage?.Invoke(msg);
    }
}

