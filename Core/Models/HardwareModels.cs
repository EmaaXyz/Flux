using System;
using System.Collections.Generic;

namespace Flux.Core.Models
{
    // ── Top-level snapshot emitted every poll cycle ──────────────────────────

    public class HardwareSnapshot
    {
        public DateTime       Timestamp    { get; set; }
        public float          TotalCpuLoad { get; set; }   // 0–100 %
        public List<float>    CoreLoads    { get; set; } = new();
        public RamInfo        RamInfo      { get; set; } = new();
        public List<GpuMetric> GpuMetrics  { get; set; } = new();
    }

    // ── RAM ──────────────────────────────────────────────────────────────────

    public class RamInfo
    {
        public float TotalMb      { get; set; }
        public float UsedMb       { get; set; }
        public float AvailableMb  { get; set; }
        public float UsagePercent { get; set; }   // 0–100 %

        public float TotalGb      => TotalMb     / 1024f;
        public float UsedGb       => UsedMb      / 1024f;
        public float AvailableGb  => AvailableMb / 1024f;
    }

    // ── GPU ──────────────────────────────────────────────────────────────────

    public class GpuMetric
    {
        public string Name                 { get; set; } = string.Empty;
        public long   AdapterRamMb         { get; set; }
        public string DriverVersion        { get; set; } = string.Empty;
        public float  TemperatureCelsius   { get; set; }   // -1 = unavailable
        public int    FanSpeedRpm          { get; set; }   // -1 = unavailable
        public float  LoadPercent          { get; set; }   // 0–100 %, via vendor SDK
        public float  VramUsedMb           { get; set; }
    }

    // ── Process info (for Gaming Mode kill-list) ─────────────────────────────

    public class ProcessInfo
    {
        public int    Pid         { get; set; }
        public string Name        { get; set; } = string.Empty;
        public float  CpuPercent  { get; set; }
        public long   MemoryMb    { get; set; }
        public bool   IsEssential { get; set; }
    }

    // ── Gaming Mode state ────────────────────────────────────────────────────

    public enum GamingModeState { Off, Activating, Active, Deactivating }

    public class GamingModeSession
    {
        public DateTime         StartedAt          { get; set; }
        public List<int>        KilledProcessPids  { get; set; } = new();
        public string           PreviousPowerPlan  { get; set; } = string.Empty;
        public GamingModeState  State              { get; set; }
    }
}
