using System.Runtime.InteropServices;
using SysManager.Native;
using static SysManager.Native.NativeMethods;

namespace SysManager.Services;

/// <summary>
/// Bluetooth device information model
/// </summary>
public class BluetoothDevice
{
    public ulong Address { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public bool IsPaired { get; set; }
    public bool IsAuthenticated { get; set; }
    public DateTime LastSeen { get; set; }
    public DateTime LastUsed { get; set; }
    public uint ClassOfDevice { get; set; }

    public string AddressString => $"{(Address >> 40) & 0xFF:X2}:{(Address >> 32) & 0xFF:X2}:{(Address >> 24) & 0xFF:X2}:{(Address >> 16) & 0xFF:X2}:{(Address >> 8) & 0xFF:X2}:{Address & 0xFF:X2}";

    public string DeviceType => GetDeviceType(ClassOfDevice);

    private static string GetDeviceType(uint classOfDevice)
    {
        // Major Device Class (bits 8-12)
        uint majorClass = (classOfDevice >> 8) & 0x1F;
        return majorClass switch
        {
            0x01 => "计算机",
            0x02 => "手机",
            0x03 => "网络接入点",
            0x04 => "音频/视频",
            0x05 => "外设",
            0x06 => "图像设备",
            0x07 => "可穿戴设备",
            0x08 => "玩具",
            0x09 => "健康设备",
            _ => "其他设备"
        };
    }

    public string StatusText => IsConnected ? "已连接" : (IsPaired ? "已配对" : "可用");
}

/// <summary>
/// Bluetooth radio (adapter) information model
/// </summary>
public class BluetoothRadio
{
    public IntPtr Handle { get; set; }
    public string Name { get; set; } = string.Empty;
    public ulong Address { get; set; }
    public bool IsConnectable { get; set; }
    public bool IsDiscoverable { get; set; }

    public string AddressString => $"{(Address >> 40) & 0xFF:X2}:{(Address >> 32) & 0xFF:X2}:{(Address >> 24) & 0xFF:X2}:{(Address >> 16) & 0xFF:X2}:{(Address >> 8) & 0xFF:X2}:{Address & 0xFF:X2}";
}

/// <summary>
/// Bluetooth management service using Windows Bluetooth API
/// </summary>
public class BluetoothService : IDisposable
{
    private readonly List<IntPtr> _radioHandles = new();
    private bool _disposed;

    public event EventHandler<string>? BluetoothStatusChanged;

    /// <summary>
    /// Check if Bluetooth is available on this system
    /// </summary>
    public bool IsBluetoothAvailable()
    {
        var radios = GetRadios();
        return radios.Count > 0;
    }

    /// <summary>
    /// Get all Bluetooth radios (adapters)
    /// </summary>
    public List<BluetoothRadio> GetRadios()
    {
        var radios = new List<BluetoothRadio>();

        // Close any previously opened handles
        CloseAllRadioHandles();

        var findParams = BLUETOOTH_FIND_RADIO_PARAMS.Create();
        IntPtr hFind = BluetoothFindFirstRadio(ref findParams, out IntPtr hRadio);

        if (hFind == IntPtr.Zero)
        {
            return radios;
        }

        try
        {
            do
            {
                if (hRadio != IntPtr.Zero)
                {
                    _radioHandles.Add(hRadio);
                    var radioInfo = BLUETOOTH_RADIO_INFO.Create();
                    uint result = BluetoothGetRadioInfo(hRadio, ref radioInfo);

                    if (result == 0)
                    {
                        radios.Add(new BluetoothRadio
                        {
                            Handle = hRadio,
                            Name = radioInfo.szName,
                            Address = radioInfo.address,
                            IsConnectable = BluetoothIsConnectable(hRadio),
                            IsDiscoverable = BluetoothIsDiscoverable(hRadio)
                        });
                    }
                }
            } while (BluetoothFindNextRadio(hFind, out hRadio));
        }
        finally
        {
            BluetoothFindRadioClose(hFind);
        }

        return radios;
    }

    /// <summary>
    /// Get all paired and discovered Bluetooth devices
    /// </summary>
    public List<BluetoothDevice> GetDevices()
    {
        var devices = new List<BluetoothDevice>();
        var radios = GetRadios();

        if (radios.Count == 0)
        {
            return devices;
        }

        foreach (var radio in radios)
        {
            var searchParams = BLUETOOTH_DEVICE_SEARCH_PARAMS.Create(radio.Handle);
            var deviceInfo = BLUETOOTH_DEVICE_INFO.Create();

            IntPtr hFind = BluetoothFindFirstDevice(ref searchParams, ref deviceInfo);
            if (hFind == IntPtr.Zero)
            {
                continue;
            }

            try
            {
                do
                {
                    devices.Add(new BluetoothDevice
                    {
                        Address = deviceInfo.Address,
                        Name = string.IsNullOrEmpty(deviceInfo.szName) ? "Unknown Device" : deviceInfo.szName,
                        IsConnected = deviceInfo.fConnected,
                        IsPaired = deviceInfo.fRemembered,
                        IsAuthenticated = deviceInfo.fAuthenticated,
                        LastSeen = SystemTimeToDateTime(deviceInfo.stLastSeen),
                        LastUsed = SystemTimeToDateTime(deviceInfo.stLastUsed),
                        ClassOfDevice = deviceInfo.ulClassofDevice
                    });

                    deviceInfo = BLUETOOTH_DEVICE_INFO.Create();
                } while (BluetoothFindNextDevice(hFind, ref deviceInfo));
            }
            finally
            {
                BluetoothFindDeviceClose(hFind);
            }
        }

        return devices.OrderByDescending(d => d.IsConnected)
                      .ThenByDescending(d => d.IsPaired)
                      .ThenBy(d => d.Name)
                      .ToList();
    }

    /// <summary>
    /// Get only paired devices
    /// </summary>
    public List<BluetoothDevice> GetPairedDevices()
    {
        return GetDevices().Where(d => d.IsPaired).ToList();
    }

    /// <summary>
    /// Get connected devices
    /// </summary>
    public List<BluetoothDevice> GetConnectedDevices()
    {
        return GetDevices().Where(d => d.IsConnected).ToList();
    }

    /// <summary>
    /// Enable or disable Bluetooth discovery mode
    /// </summary>
    public bool SetDiscoverable(bool enable)
    {
        var radios = GetRadios();
        if (radios.Count == 0)
        {
            return false;
        }

        bool success = true;
        foreach (var radio in radios)
        {
            if (!BluetoothEnableDiscovery(radio.Handle, enable))
            {
                success = false;
            }
        }

        if (success)
        {
            BluetoothStatusChanged?.Invoke(this, enable ? "蓝牙已设为可发现" : "蓝牙已设为不可发现");
        }

        return success;
    }

    /// <summary>
    /// Enable or disable Bluetooth incoming connections
    /// </summary>
    public bool SetConnectable(bool enable)
    {
        var radios = GetRadios();
        if (radios.Count == 0)
        {
            return false;
        }

        bool success = true;
        foreach (var radio in radios)
        {
            if (!BluetoothEnableIncomingConnections(radio.Handle, enable))
            {
                success = false;
            }
        }

        if (success)
        {
            BluetoothStatusChanged?.Invoke(this, enable ? "蓝牙已启用连接" : "蓝牙已禁用连接");
        }

        return success;
    }

    /// <summary>
    /// Check if Bluetooth is currently discoverable
    /// </summary>
    public bool IsDiscoverable()
    {
        var radios = GetRadios();
        return radios.Any(r => r.IsDiscoverable);
    }

    /// <summary>
    /// Check if Bluetooth is currently connectable
    /// </summary>
    public bool IsConnectable()
    {
        var radios = GetRadios();
        return radios.Any(r => r.IsConnectable);
    }

    /// <summary>
    /// Open Windows Bluetooth settings
    /// </summary>
    public void OpenBluetoothSettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-settings:bluetooth",
                UseShellExecute = true
            });
        }
        catch
        {
            // Fallback to control panel
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "control",
                Arguments = "bthprops.cpl",
                UseShellExecute = true
            });
        }
    }

    /// <summary>
    /// Open device pairing wizard
    /// </summary>
    public void OpenPairingWizard()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "fsquirt.exe",
                UseShellExecute = true
            });
        }
        catch
        {
            OpenBluetoothSettings();
        }
    }

    private static DateTime SystemTimeToDateTime(SYSTEMTIME st)
    {
        try
        {
            if (st.wYear == 0) return DateTime.MinValue;
            return new DateTime(st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private void CloseAllRadioHandles()
    {
        foreach (var handle in _radioHandles)
        {
            if (handle != IntPtr.Zero)
            {
                CloseHandle(handle);
            }
        }
        _radioHandles.Clear();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            CloseAllRadioHandles();
            _disposed = true;
        }
    }

    ~BluetoothService()
    {
        Dispose(false);
    }
}
