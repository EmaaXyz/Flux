using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Flux.Core.Models;

namespace Flux.Core.Engine
{
    /// <summary>
    /// Central hardware monitoring engine.
    /// Polls CPU, RAM, GPU metrics at configurable intervals.
    /// Raises HardwareDataUpdated event for UI binding.
    /// </summary>
    public sealed class HardwareEngine : IDisposable
    {
        // ── Win32 P/Invoke ──────────────────────────────────────────────────

        [DllImport("kernel32.dll")]
        private static extern bool GetSystemTimes(
            out FILETIME lpIdleTime,
            out FILETIME lpKernelTime,
            out FILETIME lpUserTime);

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME { public uint Low; public uint High; }

        private static ulong ToUlong(FILETIME ft) =>
            ((ulong)ft.High << 32) | ft.Low;

        // ── Fields ──────────────────────────────────────────────────────────

        private readonly int                    _pollingMs;
        private          CancellationTokenSource _cts = new();
        private          Task?                  _pollingTask;

        // Per-core CPU counters
        private readonly List<PerformanceCounter> _coreCpuCounters = new();
        private readonly PerformanceCounter       _totalCpuCounter;
        private readonly PerformanceCounter       _ramCounter;

        // Previous FILETIME snapshots for manual CPU calculation (fallback)
        private ulong _prevIdleTime, _prevKernelTime, _prevUserTime;

        // ── Events ──────────────────────────────────────────────────────────

        public event EventHandler<HardwareSnapshot>? HardwareDataUpdated;

        // ── Constructor ─────────────────────────────────────────────────────

        public HardwareEngine(int pollingIntervalMs = 1000)
        {
            _pollingMs = pollingIntervalMs;

            // Total CPU counter
            _totalCpuCounter = new PerformanceCounter(
                "Processor", "% Processor Time", "_Total", true);

            // RAM available counter
            _ramCounter = new PerformanceCounter(
                "Memory", "Available MBytes", true);

            // Per-core counters
            InitializeCoreCounters();
        }

        // ── Initialisation ──────────────────────────────────────────────────

        private void InitializeCoreCounters()
        {
            try
            {
                var cat = new PerformanceCounterCategory("Processor");
                string[] instances = cat.GetInstanceNames();

                foreach (string instance in instances)
                {
                    if (instance == "_Total") continue;
                    _coreCpuCounters.Add(new PerformanceCounter(
                        "Processor", "% Processor Time", instance, true));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HardwareEngine] Core counter init failed: {ex.Message}");
            }
        }

        // ── Public API ──────────────────────────────────────────────────────

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _pollingTask = Task.Run(() => PollLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts.Cancel();
            _pollingTask?.Wait(2000);
        }

        // ── Polling Loop ────────────────────────────────────────────────────

        private async Task PollLoop(CancellationToken ct)
        {
            // First read — discard (counters need a baseline tick)
            _ = _totalCpuCounter.NextValue();
            foreach (var c in _coreCpuCounters) _ = c.NextValue();
            await Task.Delay(1000, ct);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var snapshot = await BuildSnapshotAsync();
                    HardwareDataUpdated?.Invoke(this, snapshot);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HardwareEngine] Poll error: {ex.Message}");
                }

                await Task.Delay(_pollingMs, ct);
            }
        }

        // ── Snapshot Assembly ───────────────────────────────────────────────

        private async Task<HardwareSnapshot> BuildSnapshotAsync()
        {
            var snapshot = new HardwareSnapshot
            {
                Timestamp    = DateTime.Now,
                TotalCpuLoad = ReadTotalCpu(),
                CoreLoads    = ReadCoreLoads(),
                RamInfo      = ReadRam(),
                GpuMetrics   = await ReadGpuAsync(),
            };
            return snapshot;
        }

        // ── CPU ─────────────────────────────────────────────────────────────

        private float ReadTotalCpu()
        {
            try { return _totalCpuCounter.NextValue(); }
            catch { return ReadCpuViaFiletime(); }
        }

        private float ReadCpuViaFiletime()
        {
            GetSystemTimes(out var idle, out var kernel, out var user);
            ulong idleT   = ToUlong(idle);
            ulong kernelT = ToUlong(kernel);
            ulong userT   = ToUlong(user);

            ulong sysTotal = (kernelT - _prevKernelTime) + (userT - _prevUserTime);
            ulong sysIdle  = idleT - _prevIdleTime;

            _prevIdleTime   = idleT;
            _prevKernelTime = kernelT;
            _prevUserTime   = userT;

            if (sysTotal == 0) return 0f;
            return (float)((sysTotal - sysIdle) * 100.0 / sysTotal);
        }

        private List<float> ReadCoreLoads()
        {
            var loads = new List<float>();
            foreach (var counter in _coreCpuCounters)
            {
                try { loads.Add(counter.NextValue()); }
                catch { loads.Add(0f); }
            }
            return loads;
        }

        // ── RAM ─────────────────────────────────────────────────────────────

        private RamInfo ReadRam()
        {
            try
            {
                float availableMb = _ramCounter.NextValue();
                long  totalBytes  = GetTotalPhysicalMemory();
                float totalMb     = totalBytes / (1024f * 1024f);
                float usedMb      = totalMb - availableMb;

                return new RamInfo
                {
                    TotalMb     = totalMb,
                    UsedMb      = usedMb,
                    AvailableMb = availableMb,
                    UsagePercent = totalMb > 0 ? (usedMb / totalMb) * 100f : 0f
                };
            }
            catch { return new RamInfo(); }
        }

        private static long GetTotalPhysicalMemory()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in searcher.Get())
                    return Convert.ToInt64(obj["TotalPhysicalMemory"]);
            }
            catch { /* fallback */ }
            return 0;
        }

        // ── GPU (via WMI — works with any GPU brand) ─────────────────────────

        private static async Task<List<GpuMetric>> ReadGpuAsync()
        {
            return await Task.Run(() =>
            {
                var results = new List<GpuMetric>();
                try
                {
                    // GPU basic info via WMI
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_VideoController");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        results.Add(new GpuMetric
                        {
                            Name          = obj["Name"]?.ToString() ?? "Unknown GPU",
                            AdapterRamMb  = Convert.ToInt64(obj["AdapterRAM"] ?? 0) / (1024 * 1024),
                            DriverVersion = obj["DriverVersion"]?.ToString() ?? "N/A",
                            // Temperature requires LibreHardwareMonitor or NvAPI/ADLX
                            // See GpuSensorService for extended sensor reads
                            TemperatureCelsius = TryReadGpuTempFromWmi(),
                            FanSpeedRpm        = -1 // Requires vendor SDK
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HardwareEngine] GPU read failed: {ex.Message}");
                }
                return results;
            });
        }

        private static float TryReadGpuTempFromWmi()
        {
            // MSAcpi_ThermalZoneTemperature gives system temps (not always GPU)
            // For accurate GPU temp: integrate LibreHardwareMonitor NuGet package
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
                foreach (ManagementObject obj in searcher.Get())
                {
                    double rawTemp = Convert.ToDouble(obj["CurrentTemperature"]);
                    return (float)((rawTemp / 10.0) - 273.15); // Kelvin → Celsius
                }
            }
            catch { /* no WMI thermal support */ }
            return -1f;
        }

        // ── IDisposable ─────────────────────────────────────────────────────

        public void Dispose()
        {
            Stop();
            _totalCpuCounter.Dispose();
            _ramCounter.Dispose();
            foreach (var c in _coreCpuCounters) c.Dispose();
            _cts.Dispose();
        }
    }
}