using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using SysManager.Helpers;
using SysManager.ViewModels;

namespace SysManager.Views;

/// <summary>
/// MainWindow.xaml code-behind
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = (MainViewModel)DataContext;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply Aero glass effect
        AeroHelper.EnableBlur(this);
        
        // Position window near the system tray
        PositionNearTray();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Hide window and popup when it loses focus
        WifiListPopup.IsOpen = false;
        this.Hide();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            this.DragMove();
        }
    }

    /// <summary>
    /// Show WiFi list popup
    /// </summary>
    private void AvailableNetworksBtn_Click(object sender, RoutedEventArgs e)
    {
        BluetoothListPopup.IsOpen = false;
        WifiListPopup.IsOpen = !WifiListPopup.IsOpen;
        if (WifiListPopup.IsOpen)
        {
            _viewModel.RefreshNetworks();
        }
    }

    /// <summary>
    /// Show Bluetooth devices popup
    /// </summary>
    private void BluetoothDevicesBtn_Click(object sender, RoutedEventArgs e)
    {
        WifiListPopup.IsOpen = false;
        BluetoothListPopup.IsOpen = !BluetoothListPopup.IsOpen;
        if (BluetoothListPopup.IsOpen)
        {
            _viewModel.RefreshBluetooth();
        }
    }

    /// <summary>
    /// Position the window near the system tray based on taskbar position
    /// </summary>
    public void PositionNearTray()
    {
        // 获取任务栏位置并相应定位窗口
        var position = TaskbarHelper.GetWindowPosition(this.Width, this.ActualHeight);
        
        this.Left = position.X;
        this.Top = position.Y;
        
        // 确保窗口在屏幕上可见
        AdjustWindowToScreenBounds();
    }

    /// <summary>
    /// Adjust window position to ensure it's within screen bounds
    /// </summary>
    private void AdjustWindowToScreenBounds()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        
        // 确保窗口不会超出屏幕边界
        if (this.Left < 0) this.Left = 0;
        if (this.Top < 0) this.Top = 0;
        if (this.Left + this.Width > screenWidth) this.Left = screenWidth - this.Width;
        if (this.Top + this.ActualHeight > screenHeight) this.Top = screenHeight - this.ActualHeight;
    }

    /// <summary>
    /// Show and activate the window, refreshing data
    /// </summary>
    public void ShowAndActivate()
    {
        _viewModel.RefreshAll();
        this.Show();
        this.UpdateLayout(); // Ensure layout is updated before positioning
        PositionNearTray();
        this.Activate();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
