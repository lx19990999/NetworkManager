using System.Windows;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;
using SysManager.ViewModels;
using SysManager.Views;
using WpfApplication = System.Windows.Application;
using DrawingIcon = System.Drawing.Icon;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingColor = System.Drawing.Color;
using DrawingSolidBrush = System.Drawing.SolidBrush;
using DrawingPen = System.Drawing.Pen;

namespace SysManager;

/// <summary>
/// Application entry point with system tray functionality
/// </summary>
public partial class App : WpfApplication
{
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Create main window (hidden initially)
        _mainWindow = new MainWindow();
        _mainWindow.Hide();

        // Setup tray icon
        SetupTrayIcon();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        
        if (_trayIcon != null)
        {
            // Set icon
            _trayIcon.Icon = CreateDefaultIcon();
            
            // Set data context for context menu commands
            _trayIcon.DataContext = new TrayIconViewModel(this);
            
            // 直接处理点击事件 - 左键单击直接弹出窗口
            _trayIcon.TrayLeftMouseUp += TrayIcon_TrayLeftMouseUp;
            _trayIcon.TrayMouseDoubleClick += TrayIcon_TrayMouseDoubleClick;
        }
    }

    /// <summary>
    /// 左键点击托盘图标 - 直接显示窗口
    /// </summary>
    private void TrayIcon_TrayLeftMouseUp(object sender, RoutedEventArgs e)
    {
        ShowMainWindow();
    }

    /// <summary>
    /// 双击托盘图标 - 直接显示窗口
    /// </summary>
    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        ShowMainWindow();
    }

    /// <summary>
    /// Create a simple default icon if resource is not available
    /// </summary>
    private static DrawingIcon CreateDefaultIcon()
    {
        try
        {
            // Try to load from resources
            var iconUri = new Uri("pack://application:,,,/Resources/app.ico", UriKind.Absolute);
            using var stream = System.Windows.Application.GetResourceStream(iconUri)?.Stream;
            if (stream != null)
            {
                return new DrawingIcon(stream);
            }
        }
        catch { }

        // Create a simple icon programmatically - WiFi style icon
        using var bitmap = new DrawingBitmap(32, 32);
        using var g = DrawingGraphics.FromImage(bitmap);
        
        // Enable anti-aliasing
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        // Draw background circle
        g.Clear(DrawingColor.Transparent);
        using var bgBrush = new DrawingSolidBrush(DrawingColor.FromArgb(255, 0, 120, 212)); // Windows blue
        g.FillEllipse(bgBrush, 2, 2, 28, 28);
        
        // Draw WiFi signal arcs
        using var whitePen = new DrawingPen(DrawingColor.White, 2.5f);
        g.DrawArc(whitePen, 6, 8, 20, 20, 225, 90);
        g.DrawArc(whitePen, 9, 11, 14, 14, 225, 90);
        g.DrawArc(whitePen, 12, 14, 8, 8, 225, 90);
        
        // Draw center dot
        using var dotBrush = new DrawingSolidBrush(DrawingColor.White);
        g.FillEllipse(dotBrush, 14, 20, 4, 4);
        
        var handle = bitmap.GetHicon();
        return DrawingIcon.FromHandle(handle);
    }

    public void ShowMainWindow()
    {
        if (_mainWindow != null)
        {
            // 如果窗口已经显示，则隐藏它（切换行为）
            if (_mainWindow.IsVisible)
            {
                _mainWindow.Hide();
            }
            else
            {
                _mainWindow.ShowAndActivate();
            }
        }
    }

    public void ExitApplication()
    {
        _trayIcon?.Dispose();
        _mainWindow?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}

/// <summary>
/// ViewModel for tray icon context menu
/// </summary>
public class TrayIconViewModel
{
    private readonly App _app;

    public TrayIconViewModel(App app)
    {
        _app = app;

        ExitCommand = new RelayCommand(() => _app.ExitApplication());
        
        OpenNetworkSettingsCommand = new RelayCommand(() =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-settings:network-wifi",
                    UseShellExecute = true
                });
            }
            catch { }
        });

        OpenBluetoothSettingsCommand = new RelayCommand(() =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-settings:bluetooth",
                    UseShellExecute = true
                });
            }
            catch { }
        });

        OpenSoundSettingsCommand = new RelayCommand(() =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-settings:sound",
                    UseShellExecute = true
                });
            }
            catch { }
        });
    }

    public ICommand ExitCommand { get; }
    public ICommand OpenNetworkSettingsCommand { get; }
    public ICommand OpenBluetoothSettingsCommand { get; }
    public ICommand OpenSoundSettingsCommand { get; }
}
