using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace Flux.Core.Services
{
    /// <summary>
    /// Wraps LibreHardwareMonitor for accurate cross-vendor sensor readings:
    /// CPU core temps, GPU temp + load + VRAM, fan RPMs, motherboard sensors.
    ///
    /// REQUIRES: App running as Administrator (ring-0 driver access).
    ///
    /// Usage:
    ///   var svc = new GpuSensorService();
    ///   svc.Initialize();
    ///   var temps = svc.GetGpuTemperatures();  // call on each poll tick
    ///   svc.Dispose();
    /// </summary>
    public sealed class HardwareSensorService : IDisposable
    {
        private readonly Computer _computer;
        private bool _initialized;

        public HardwareSensorService()
        {
            _computer = new Computer
            {
                IsCpuEnabled          = true,
                IsGpuEnabled          = true,
                IsMemoryEnabled       = true,
                IsMotherboardEnabled  = true,
                IsStorageEnabled      = false,   // enable if you need SSD temps
                IsNetworkEnabled      = false
            };
        }

        public void Initialize()
        {
            _computer.Open();
            _computer.Accept(new UpdateVisitor());
            _initialized = true;
        }

        // ── Per-call update ────────────────────────────────────────────────

        public void Update()
        {
            if (!_initialized) return;
            _computer.Accept(new UpdateVisitor());
        }

        // ── CPU Temperatures ───────────────────────────────────────────────

        public List<SensorReading> GetCpuTemperatures()
        {
            return GetReadings(HardwareType.Cpu, SensorType.Temperature);
        }

        public float GetCpuPackageTemp()
        {
            return GetCpuTemperatures()
                .FirstOrDefault(r => r.Name.Contains("Package") ||
                                     r.Name.Contains("Tctl"))?.Value ?? -1f;
        }

        // ── GPU Sensors ────────────────────────────────────────────────────

        public List<SensorReading> GetGpuTemperatures()
        {
            return GetReadings(HardwareType.GpuNvidia, SensorType.Temperature)
                .Concat(GetReadings(HardwareType.GpuAmd, SensorType.Temperature))
                .ToList();
        }

        public float GetPrimaryGpuTemp()
        {
            var temps = GetGpuTemperatures();
            return temps.FirstOrDefault(r => r.Name.Contains("Core") ||
                                             r.Name.Contains("GPU Core"))?.Value
                   ?? temps.FirstOrDefault()?.Value ?? -1f;
        }

        public List<SensorReading> GetGpuLoads()
        {
            return GetReadings(HardwareType.GpuNvidia, SensorType.Load)
                .Concat(GetReadings(HardwareType.GpuAmd, SensorType.Load))
                .ToList();
        }

        public List<SensorReading> GetGpuFanSpeeds()
        {
            return GetReadings(HardwareType.GpuNvidia, SensorType.Fan)
                .Concat(GetReadings(HardwareType.GpuAmd, SensorType.Fan))
                .ToList();
        }

        // ── Fan Speeds (all) ────────────────────────────────────────────────

        public List<SensorReading> GetAllFanSpeeds()
        {
            var fans = new List<SensorReading>();
            fans.AddRange(GetReadings(HardwareType.Motherboard, SensorType.Fan));
            fans.AddRange(GetReadings(HardwareType.Cpu, SensorType.Fan));
            fans.AddRange(GetGpuFanSpeeds());
            return fans;
        }

        // ── Generic reader ──────────────────────────────────────────────────

        private List<SensorReading> GetReadings(HardwareType hwType, SensorType sType)
        {
            if (!_initialized) return new();
            var results = new List<SensorReading>();

            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == hwType))
            {
                hw.Update();
                foreach (var sensor in hw.Sensors.Where(s => s.SensorType == sType))
                {
                    if (sensor.Value.HasValue)
                        results.Add(new SensorReading
                        {
                            Name         = sensor.Name,
                            Value        = sensor.Value.Value,
                            SensorType   = sType.ToString(),
                            HardwareName = hw.Name
                        });
                }
                // Sub-hardware (e.g., Embedded Controller fans)
                foreach (var sub in hw.SubHardware)
                {
                    sub.Update();
                    foreach (var s in sub.Sensors.Where(s => s.SensorType == sType))
                    {
                        if (s.Value.HasValue)
                            results.Add(new SensorReading
                            {
                                Name         = s.Name,
                                Value        = s.Value.Value,
                                SensorType   = sType.ToString(),
                                HardwareName = $"{hw.Name} / {sub.Name}"
                            });
                    }
                }
            }
            return results;
        }

        // ── UpdateVisitor helper ────────────────────────────────────────────

        private sealed class UpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer c) => c.Traverse(this);
            public void VisitHardware(IHardware h) { h.Update(); h.Traverse(this); }
            public void VisitSensor(ISensor s) { }
            public void VisitParameter(IParameter p) { }
        }

        // ── IDisposable ─────────────────────────────────────────────────────

        public void Dispose() => _computer.Close();
    }

    // ── DTO ──────────────────────────────────────────────────────────────────

    public record SensorReading
    {
        public string Name         { get; init; } = string.Empty;
        public float  Value        { get; init; }
        public string SensorType   { get; init; } = string.Empty;
        public string HardwareName { get; init; } = string.Empty;
    }
}
