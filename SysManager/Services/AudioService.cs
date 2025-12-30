using System.Runtime.InteropServices;
using SysManager.Native;
using static SysManager.Native.NativeMethods;

namespace SysManager.Services;

/// <summary>
/// Audio device information model
/// </summary>
public class AudioDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public float Volume { get; set; }
    public bool IsMuted { get; set; }

    public int VolumePercent => (int)(Volume * 100);
}

/// <summary>
/// Audio/Volume control service using Windows Core Audio API
/// </summary>
public class AudioService : IDisposable
{
    private IMMDeviceEnumerator? _deviceEnumerator;
    private IAudioEndpointVolume? _volumeControl;
    private bool _disposed;

    public event EventHandler<int>? VolumeChanged;
    public event EventHandler<bool>? MuteChanged;

    public AudioService()
    {
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            _deviceEnumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            UpdateVolumeControl();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize AudioService: {ex.Message}");
        }
    }

    private void UpdateVolumeControl()
    {
        if (_deviceEnumerator == null) return;

        try
        {
            int hr = _deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out IMMDevice device);
            if (hr != 0 || device == null) return;

            Guid iid = IID_IAudioEndpointVolume;
            hr = device.Activate(ref iid, 0, IntPtr.Zero, out object activated);
            if (hr == 0 && activated != null)
            {
                _volumeControl = (IAudioEndpointVolume)activated;
            }

            Marshal.ReleaseComObject(device);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get volume control: {ex.Message}");
        }
    }

    /// <summary>
    /// Get current master volume level (0-100)
    /// </summary>
    public int GetVolume()
    {
        if (_volumeControl == null)
        {
            UpdateVolumeControl();
            if (_volumeControl == null) return 50;
        }

        try
        {
            int hr = _volumeControl.GetMasterVolumeLevelScalar(out float level);
            if (hr == 0)
            {
                return (int)(level * 100);
            }
        }
        catch { }

        return 50;
    }

    /// <summary>
    /// Set master volume level (0-100)
    /// </summary>
    public bool SetVolume(int volume)
    {
        if (_volumeControl == null)
        {
            UpdateVolumeControl();
            if (_volumeControl == null) return false;
        }

        volume = Math.Clamp(volume, 0, 100);
        float level = volume / 100f;

        try
        {
            Guid eventContext = Guid.Empty;
            int hr = _volumeControl.SetMasterVolumeLevelScalar(level, ref eventContext);
            if (hr == 0)
            {
                VolumeChanged?.Invoke(this, volume);
                return true;
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Increase volume by specified amount
    /// </summary>
    public bool IncreaseVolume(int amount = 5)
    {
        int current = GetVolume();
        return SetVolume(current + amount);
    }

    /// <summary>
    /// Decrease volume by specified amount
    /// </summary>
    public bool DecreaseVolume(int amount = 5)
    {
        int current = GetVolume();
        return SetVolume(current - amount);
    }

    /// <summary>
    /// Check if audio is muted
    /// </summary>
    public bool IsMuted()
    {
        if (_volumeControl == null)
        {
            UpdateVolumeControl();
            if (_volumeControl == null) return false;
        }

        try
        {
            int hr = _volumeControl.GetMute(out bool muted);
            if (hr == 0)
            {
                return muted;
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Set mute state
    /// </summary>
    public bool SetMute(bool mute)
    {
        if (_volumeControl == null)
        {
            UpdateVolumeControl();
            if (_volumeControl == null) return false;
        }

        try
        {
            Guid eventContext = Guid.Empty;
            int hr = _volumeControl.SetMute(mute, ref eventContext);
            if (hr == 0)
            {
                MuteChanged?.Invoke(this, mute);
                return true;
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Toggle mute state
    /// </summary>
    public bool ToggleMute()
    {
        bool currentMute = IsMuted();
        return SetMute(!currentMute);
    }

    /// <summary>
    /// Get volume step information
    /// </summary>
    public (int currentStep, int totalSteps) GetVolumeSteps()
    {
        if (_volumeControl == null)
        {
            UpdateVolumeControl();
            if (_volumeControl == null) return (0, 20);
        }

        try
        {
            int hr = _volumeControl.GetVolumeStepInfo(out uint step, out uint stepCount);
            if (hr == 0)
            {
                return ((int)step, (int)stepCount);
            }
        }
        catch { }

        return (0, 20);
    }

    /// <summary>
    /// Step volume up
    /// </summary>
    public bool VolumeStepUp()
    {
        if (_volumeControl == null)
        {
            UpdateVolumeControl();
            if (_volumeControl == null) return false;
        }

        try
        {
            Guid eventContext = Guid.Empty;
            int hr = _volumeControl.VolumeStepUp(ref eventContext);
            if (hr == 0)
            {
                VolumeChanged?.Invoke(this, GetVolume());
                return true;
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Step volume down
    /// </summary>
    public bool VolumeStepDown()
    {
        if (_volumeControl == null)
        {
            UpdateVolumeControl();
            if (_volumeControl == null) return false;
        }

        try
        {
            Guid eventContext = Guid.Empty;
            int hr = _volumeControl.VolumeStepDown(ref eventContext);
            if (hr == 0)
            {
                VolumeChanged?.Invoke(this, GetVolume());
                return true;
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Get volume range in decibels
    /// </summary>
    public (float minDb, float maxDb, float incrementDb) GetVolumeRange()
    {
        if (_volumeControl == null)
        {
            UpdateVolumeControl();
            if (_volumeControl == null) return (-96f, 0f, 0.5f);
        }

        try
        {
            int hr = _volumeControl.GetVolumeRange(out float min, out float max, out float increment);
            if (hr == 0)
            {
                return (min, max, increment);
            }
        }
        catch { }

        return (-96f, 0f, 0.5f);
    }

    /// <summary>
    /// Open Windows sound settings
    /// </summary>
    public void OpenSoundSettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-settings:sound",
                UseShellExecute = true
            });
        }
        catch
        {
            // Fallback to control panel
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "control",
                    Arguments = "mmsys.cpl sounds",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    /// <summary>
    /// Open volume mixer
    /// </summary>
    public void OpenVolumeMixer()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sndvol.exe",
                UseShellExecute = true
            });
        }
        catch { }
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
            if (_volumeControl != null)
            {
                Marshal.ReleaseComObject(_volumeControl);
                _volumeControl = null;
            }

            if (_deviceEnumerator != null)
            {
                Marshal.ReleaseComObject(_deviceEnumerator);
                _deviceEnumerator = null;
            }

            _disposed = true;
        }
    }

    ~AudioService()
    {
        Dispose(false);
    }
}
