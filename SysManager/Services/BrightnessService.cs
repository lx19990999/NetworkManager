using System.Management;
using System.Runtime.InteropServices;
using SysManager.Native;
using static SysManager.Native.NativeMethods;

namespace SysManager.Services;

/// <summary>
/// Monitor information model
/// </summary>
public class MonitorInfo
{
    public IntPtr Handle { get; set; }
    public string Description { get; set; } = string.Empty;
    public uint MinBrightness { get; set; }
    public uint MaxBrightness { get; set; }
    public uint CurrentBrightness { get; set; }
    public bool SupportsDDC { get; set; }

    public int BrightnessPercent => MaxBrightness > MinBrightness
        ? (int)((CurrentBrightness - MinBrightness) * 100 / (MaxBrightness - MinBrightness))
        : 0;
}

/// <summary>
/// Brightness control service supporting both DDC/CI and WMI methods
/// </summary>
public class BrightnessService : IDisposable
{
    private readonly List<PHYSICAL_MONITOR> _physicalMonitors = new();
    private bool _disposed;
    private bool _useWmi;

    public event EventHandler<int>? BrightnessChanged;

    public BrightnessService()
    {
        // Determine which method to use
        _useWmi = !InitializeDDC();
    }

    /// <summary>
    /// Initialize DDC/CI monitor control
    /// </summary>
    private bool InitializeDDC()
    {
        try
        {
            _physicalMonitors.Clear();
            var monitors = new List<IntPtr>();

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                monitors.Add(hMonitor);
                return true;
            }, IntPtr.Zero);

            foreach (var monitor in monitors)
            {
                if (GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out uint count) && count > 0)
                {
                    var physicalMonitors = new PHYSICAL_MONITOR[count];
                    if (GetPhysicalMonitorsFromHMONITOR(monitor, count, physicalMonitors))
                    {
                        _physicalMonitors.AddRange(physicalMonitors);
                    }
                }
            }

            // Test if DDC works
            if (_physicalMonitors.Count > 0)
            {
                return GetMonitorBrightness(_physicalMonitors[0].hPhysicalMonitor, out _, out _, out _);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get current brightness level (0-100)
    /// </summary>
    public int GetBrightness()
    {
        if (_useWmi)
        {
            return GetBrightnessWMI();
        }
        else
        {
            return GetBrightnessDDC();
        }
    }

    /// <summary>
    /// Set brightness level (0-100)
    /// </summary>
    public bool SetBrightness(int brightness)
    {
        brightness = Math.Clamp(brightness, 0, 100);

        bool result;
        if (_useWmi)
        {
            result = SetBrightnessWMI(brightness);
        }
        else
        {
            result = SetBrightnessDDC(brightness);
        }

        if (result)
        {
            BrightnessChanged?.Invoke(this, brightness);
        }

        return result;
    }

    /// <summary>
    /// Increase brightness by specified amount
    /// </summary>
    public bool IncreaseBrightness(int amount = 10)
    {
        int current = GetBrightness();
        return SetBrightness(current + amount);
    }

    /// <summary>
    /// Decrease brightness by specified amount
    /// </summary>
    public bool DecreaseBrightness(int amount = 10)
    {
        int current = GetBrightness();
        return SetBrightness(current - amount);
    }

    /// <summary>
    /// Get all monitors with brightness information
    /// </summary>
    public List<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();

        if (_useWmi)
        {
            // WMI typically controls integrated display
            int brightness = GetBrightnessWMI();
            monitors.Add(new MonitorInfo
            {
                Description = "内置显示器",
                MinBrightness = 0,
                MaxBrightness = 100,
                CurrentBrightness = (uint)brightness,
                SupportsDDC = false
            });
        }
        else
        {
            foreach (var pm in _physicalMonitors)
            {
                if (GetMonitorBrightness(pm.hPhysicalMonitor, out uint min, out uint current, out uint max))
                {
                    monitors.Add(new MonitorInfo
                    {
                        Handle = pm.hPhysicalMonitor,
                        Description = pm.szPhysicalMonitorDescription,
                        MinBrightness = min,
                        MaxBrightness = max,
                        CurrentBrightness = current,
                        SupportsDDC = true
                    });
                }
            }
        }

        return monitors;
    }

    /// <summary>
    /// Check if brightness control is available
    /// </summary>
    public bool IsBrightnessControlAvailable()
    {
        try
        {
            return GetBrightness() >= 0;
        }
        catch
        {
            return false;
        }
    }

    #region DDC/CI Methods

    private int GetBrightnessDDC()
    {
        if (_physicalMonitors.Count == 0) return -1;

        try
        {
            if (GetMonitorBrightness(_physicalMonitors[0].hPhysicalMonitor, out uint min, out uint current, out uint max))
            {
                if (max > min)
                {
                    return (int)((current - min) * 100 / (max - min));
                }
            }
        }
        catch { }

        return -1;
    }

    private bool SetBrightnessDDC(int brightness)
    {
        if (_physicalMonitors.Count == 0) return false;

        bool success = true;
        foreach (var pm in _physicalMonitors)
        {
            try
            {
                if (GetMonitorBrightness(pm.hPhysicalMonitor, out uint min, out uint _, out uint max))
                {
                    uint newValue = (uint)(min + (max - min) * brightness / 100);
                    if (!SetMonitorBrightness(pm.hPhysicalMonitor, newValue))
                    {
                        success = false;
                    }
                }
            }
            catch
            {
                success = false;
            }
        }

        return success;
    }

    #endregion

    #region WMI Methods (for laptop internal displays)

    private int GetBrightnessWMI()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (ManagementObject obj in searcher.Get())
            {
                return Convert.ToInt32(obj["CurrentBrightness"]);
            }
        }
        catch { }

        return 50; // Default fallback
    }

    private bool SetBrightnessWMI(int brightness)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (ManagementObject obj in searcher.Get())
            {
                obj.InvokeMethod("WmiSetBrightness", new object[] { 1, brightness });
                return true;
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Get supported brightness levels (for some displays)
    /// </summary>
    public int[] GetBrightnessLevels()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT Level FROM WmiMonitorBrightnessLevels");
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["Level"] is byte[] levels)
                {
                    return levels.Select(b => (int)b).ToArray();
                }
            }
        }
        catch { }

        // Return default levels
        return Enumerable.Range(0, 11).Select(i => i * 10).ToArray();
    }

    #endregion

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (_physicalMonitors.Count > 0)
            {
                DestroyPhysicalMonitors((uint)_physicalMonitors.Count, _physicalMonitors.ToArray());
                _physicalMonitors.Clear();
            }
            _disposed = true;
        }
    }

    ~BrightnessService()
    {
        Dispose(false);
    }
}
