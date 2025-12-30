using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using SysManager.Native;
using static SysManager.Native.NativeMethods;

namespace SysManager.Helpers;

/// <summary>
/// Windows version detection
/// </summary>
public enum WindowsVersion
{
    Windows7,
    Windows8,
    Windows10,
    Windows11,
    Unknown
}

/// <summary>
/// Backdrop type for Windows 11
/// </summary>
public enum BackdropType
{
    None = 1,
    Mica = 2,
    Acrylic = 3,
    Tabbed = 4
}

/// <summary>
/// Helper class for Windows Aero glass and modern blur effects
/// </summary>
public static class AeroHelper
{
    /// <summary>
    /// Detect current Windows version
    /// </summary>
    public static WindowsVersion GetWindowsVersion()
    {
        var version = Environment.OSVersion.Version;

        if (version.Major == 6 && version.Minor == 1)
            return WindowsVersion.Windows7;
        if (version.Major == 6 && version.Minor >= 2)
            return WindowsVersion.Windows8;
        if (version.Major == 10 && version.Build < 22000)
            return WindowsVersion.Windows10;
        if (version.Major == 10 && version.Build >= 22000)
            return WindowsVersion.Windows11;

        return WindowsVersion.Unknown;
    }

    /// <summary>
    /// Check if DWM composition is enabled
    /// </summary>
    public static bool IsDwmCompositionEnabled()
    {
        try
        {
            DwmIsCompositionEnabled(out bool enabled);
            return enabled;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Apply glass effect to a WPF window
    /// Automatically selects the best method based on Windows version
    /// </summary>
    public static void EnableBlur(Window window)
    {
        var version = GetWindowsVersion();

        switch (version)
        {
            case WindowsVersion.Windows11:
                EnableWindows11Blur(window);
                break;
            case WindowsVersion.Windows10:
                EnableWindows10Acrylic(window);
                break;
            case WindowsVersion.Windows7:
            case WindowsVersion.Windows8:
            default:
                EnableAeroGlass(window);
                break;
        }
    }

    /// <summary>
    /// Enable Windows 7 style Aero Glass effect
    /// </summary>
    public static void EnableAeroGlass(Window window)
    {
        if (!IsDwmCompositionEnabled()) return;

        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
        {
            // Handle not yet created, defer
            window.SourceInitialized += (s, e) => EnableAeroGlass(window);
            return;
        }

        try
        {
            // Make window background transparent
            window.Background = System.Windows.Media.Brushes.Transparent;

            // Extend glass frame into client area
            var margins = MARGINS.All(-1); // -1 extends to entire window
            DwmExtendFrameIntoClientArea(helper.Handle, ref margins);

            // Enable blur behind
            var hwndSource = HwndSource.FromHwnd(helper.Handle);
            if (hwndSource?.CompositionTarget != null)
            {
                hwndSource.CompositionTarget.BackgroundColor = Colors.Transparent;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enable Aero glass: {ex.Message}");
        }
    }

    /// <summary>
    /// Enable Windows 10 Acrylic blur effect
    /// </summary>
    public static void EnableWindows10Acrylic(Window window, int gradientColor = unchecked((int)0x80000000))
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
        {
            window.SourceInitialized += (s, e) => EnableWindows10Acrylic(window, gradientColor);
            return;
        }

        try
        {
            window.Background = System.Windows.Media.Brushes.Transparent;

            var hwndSource = HwndSource.FromHwnd(helper.Handle);
            if (hwndSource?.CompositionTarget != null)
            {
                hwndSource.CompositionTarget.BackgroundColor = Colors.Transparent;
            }

            var accent = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                AccentFlags = 2,
                GradientColor = gradientColor,
                AnimationId = 0
            };

            int accentSize = Marshal.SizeOf(accent);
            IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                Data = accentPtr,
                SizeOfData = accentSize
            };

            SetWindowCompositionAttribute(helper.Handle, ref data);
            Marshal.FreeHGlobal(accentPtr);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enable Windows 10 acrylic: {ex.Message}");
            // Fallback to Aero glass
            EnableAeroGlass(window);
        }
    }

    /// <summary>
    /// Enable Windows 11 Mica or Acrylic effect
    /// </summary>
    public static void EnableWindows11Blur(Window window, BackdropType backdropType = BackdropType.Acrylic)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
        {
            window.SourceInitialized += (s, e) => EnableWindows11Blur(window, backdropType);
            return;
        }

        try
        {
            window.Background = System.Windows.Media.Brushes.Transparent;

            var hwndSource = HwndSource.FromHwnd(helper.Handle);
            if (hwndSource?.CompositionTarget != null)
            {
                hwndSource.CompositionTarget.BackgroundColor = Colors.Transparent;
            }

            // Try using DWMWA_SYSTEMBACKDROP_TYPE (Windows 11 22H2+)
            int backdropValue = (int)backdropType;
            int result = DwmSetWindowAttribute(helper.Handle, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropValue, sizeof(int));

            if (result != 0)
            {
                // Fallback: try DWMWA_MICA_EFFECT for older Windows 11
                int usemica = 1;
                DwmSetWindowAttribute(helper.Handle, DWMWINDOWATTRIBUTE.DWMWA_MICA_EFFECT, ref usemica, sizeof(int));
            }

            // Extend frame margins for proper rendering
            var margins = MARGINS.All(-1);
            DwmExtendFrameIntoClientArea(helper.Handle, ref margins);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enable Windows 11 blur: {ex.Message}");
            // Fallback to Windows 10 acrylic
            EnableWindows10Acrylic(window);
        }
    }

    /// <summary>
    /// Enable blur with specific color tint
    /// </summary>
    public static void EnableBlurWithColor(Window window, System.Windows.Media.Color color, byte opacity = 128)
    {
        int gradientColor = (opacity << 24) | (color.B << 16) | (color.G << 8) | color.R;
        EnableWindows10Acrylic(window, gradientColor);
    }

    /// <summary>
    /// Set window to use dark mode title bar (Windows 10/11)
    /// </summary>
    public static void SetDarkMode(Window window, bool useDarkMode)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
        {
            window.SourceInitialized += (s, e) => SetDarkMode(window, useDarkMode);
            return;
        }

        try
        {
            int value = useDarkMode ? 1 : 0;
            DwmSetWindowAttribute(helper.Handle, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        }
        catch { }
    }

    /// <summary>
    /// Apply rounded corners for Windows 11
    /// </summary>
    public static void SetCornerPreference(Window window, int preference = 2)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
        {
            window.SourceInitialized += (s, e) => SetCornerPreference(window, preference);
            return;
        }

        try
        {
            // 0 = default, 1 = don't round, 2 = round, 3 = round small
            DwmSetWindowAttribute(helper.Handle, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }
        catch { }
    }

    /// <summary>
    /// Disable blur effects
    /// </summary>
    public static void DisableBlur(Window window)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero) return;

        try
        {
            var accent = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_DISABLED,
                AccentFlags = 0,
                GradientColor = 0,
                AnimationId = 0
            };

            int accentSize = Marshal.SizeOf(accent);
            IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                Data = accentPtr,
                SizeOfData = accentSize
            };

            SetWindowCompositionAttribute(helper.Handle, ref data);
            Marshal.FreeHGlobal(accentPtr);
        }
        catch { }
    }
}
