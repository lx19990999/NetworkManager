using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Input;
using SysManager.Services;
using WpfApplication = System.Windows.Application;
using SysManager.Logging;

namespace SysManager.ViewModels;

/// <summary>
/// Network adapter info for display
/// </summary>
public class NetworkAdapterInfo
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public bool IsWireless { get; set; }
}

/// <summary>
/// Main ViewModel for the network manager menu
/// </summary>
public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly NetworkService _networkService;
    private readonly BluetoothService _bluetoothService;
    private readonly BrightnessService _brightnessService;
    private readonly AudioService _audioService;
    private bool _disposed;

    #region Properties

    // Network Interfaces (Ethernet cards)
    private ObservableCollection<NetworkAdapterInfo> _networkInterfaces = new();
    public ObservableCollection<NetworkAdapterInfo> NetworkInterfaces
    {
        get => _networkInterfaces;
        set => SetProperty(ref _networkInterfaces, value);
    }

    // WiFi Adapter Name
    private string _wifiAdapterName = "无线网卡";
    public string WifiAdapterName
    {
        get => _wifiAdapterName;
        set => SetProperty(ref _wifiAdapterName, value);
    }

    // Bluetooth Adapter Name
    private string _bluetoothAdapterName = "蓝牙1";
    public string BluetoothAdapterName
    {
        get => _bluetoothAdapterName;
        set => SetProperty(ref _bluetoothAdapterName, value);
    }

    // WiFi Networks
    private ObservableCollection<WiFiNetwork> _wifiNetworks = new();
    public ObservableCollection<WiFiNetwork> WiFiNetworks
    {
        get => _wifiNetworks;
        set => SetProperty(ref _wifiNetworks, value);
    }

    // Bluetooth Devices
    private ObservableCollection<BluetoothDevice> _bluetoothDevices = new();
    public ObservableCollection<BluetoothDevice> BluetoothDevices
    {
        get => _bluetoothDevices;
        set => SetProperty(ref _bluetoothDevices, value);
    }

    private bool _isWiFiConnected;
    public bool IsWiFiConnected
    {
        get => _isWiFiConnected;
        set => SetProperty(ref _isWiFiConnected, value);
    }

    private bool _isBluetoothConnected;
    public bool IsBluetoothConnected
    {
        get => _isBluetoothConnected;
        set => SetProperty(ref _isBluetoothConnected, value);
    }

    private string _connectedSSID = string.Empty;
    public string ConnectedSSID
    {
        get => _connectedSSID;
        set
        {
            SetProperty(ref _connectedSSID, value);
            OnPropertyChanged(nameof(WifiConnectionStatus)); // 当ConnectedSSID变化时，通知WifiConnectionStatus也变化
        }
    }

    public string WifiConnectionStatus => string.IsNullOrEmpty(ConnectedSSID) ? "连接" : $"({ConnectedSSID}) 断开";

    public Visibility WifiStatusVisibility => string.IsNullOrEmpty(ConnectedSSID) ? Visibility.Collapsed : Visibility.Visible;

    #endregion

    #region Commands

    public ICommand ToggleConnectionCommand { get; }
    public ICommand ToggleWifiCommand { get; }
    public ICommand ToggleBluetoothCommand { get; }
    public ICommand ConnectNetworkCommand { get; }
    public ICommand ConnectBluetoothCommand { get; }
    public ICommand ConnectHiddenNetworkCommand { get; }
    public ICommand CreateWifiNetworkCommand { get; }
    public ICommand VpnConnectionCommand { get; }
    public ICommand CloseWindowCommand { get; }

    #endregion

    public MainViewModel()
    {
        // Initialize services
        _networkService = new NetworkService();
        _bluetoothService = new BluetoothService();
        _brightnessService = new BrightnessService();
        _audioService = new AudioService();

        // Initialize commands
        ToggleConnectionCommand = new RelayCommand(ToggleConnection);
        ToggleWifiCommand = new RelayCommand(ToggleWifi);
        ToggleBluetoothCommand = new RelayCommand(ToggleBluetooth);
        ConnectNetworkCommand = new RelayCommand(ConnectNetwork);
        ConnectBluetoothCommand = new RelayCommand(ConnectBluetooth);
        ConnectHiddenNetworkCommand = new RelayCommand(_ => OpenNetworkSettings());
        CreateWifiNetworkCommand = new RelayCommand(_ => OpenNetworkSettings());
        VpnConnectionCommand = new RelayCommand(_ => OpenVpnSettings());

        CloseWindowCommand = new RelayCommand(_ =>
        {
            if (WpfApplication.Current.MainWindow != null)
            {
                WpfApplication.Current.MainWindow.Hide();
            }
        });

        // Load initial data
        RefreshAll();
    }

    private void ToggleConnection(object? parameter)
    {
        if (parameter is NetworkAdapterInfo adapter)
        {
            // Toggle network adapter connection
            // This would require additional implementation
            // For now, open network settings
            OpenNetworkSettings();
        }
    }

    private void ToggleWifi(object? parameter)
    {
        if (IsWiFiConnected)
        {
            _networkService.Disconnect();
        }
        RefreshNetworks();
    }

    private void ToggleBluetooth(object? parameter)
    {
        // Toggle Bluetooth adapter
        // For now, open Bluetooth settings
        OpenBluetoothSettings();
    }

    private void ConnectNetwork(object? parameter)
    {
        if (parameter is WiFiNetwork network)
        {
            if (network.IsConnected)
            {
                // Already connected, disconnect
                _networkService.Disconnect();
            }
            else if (network.HasProfile)
            {
                // Has saved profile, connect directly
                _networkService.Connect(network.ProfileName);
            }
            else if (network.IsSecured)
            {
                // Need password - open Windows WiFi settings for now
                OpenWifiConnectDialog(network.SSID);
            }
            else
            {
                // Open network, connect directly
                _networkService.ConnectWithPassword(network.SSID, "",
                    Native.NativeMethods.DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_80211_OPEN,
                    Native.NativeMethods.DOT11_CIPHER_ALGORITHM.DOT11_CIPHER_ALGO_NONE);
            }
            RefreshNetworks();
        }
    }

    private void ConnectBluetooth(object? parameter)
    {
        if (parameter is BluetoothDevice device)
        {
            if (device.IsConnected)
            {
                // Already connected, disconnect
                _bluetoothService.OpenBluetoothSettings();
            }
            else if (device.IsPaired)
            {
                // Device is paired, connect directly
                _bluetoothService.OpenBluetoothSettings();
            }
            else
            {
                // Need to pair first
                _bluetoothService.OpenPairingWizard();
            }
            RefreshBluetooth();
        }
    }

    private void OpenWifiConnectDialog(string ssid)
    {
        try
        {
            // Open Windows WiFi flyout
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-availablenetworks:",
                UseShellExecute = true
            });
        }
        catch
        {
            OpenNetworkSettings();
        }
    }

    private void OpenNetworkSettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-settings:network-wifi",
                UseShellExecute = true
            });
        }
        catch
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ncpa.cpl",
                UseShellExecute = true
            });
        }
    }

    private void OpenBluetoothSettings()
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
            _bluetoothService.OpenBluetoothSettings();
        }
    }

    private void OpenVpnSettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-settings:network-vpn",
                UseShellExecute = true
            });
        }
        catch { }
    }

    public void RefreshAll()
    {
        Logger.Log("开始刷新所有数据");
        LoadNetworkAdapters();
        RefreshNetworks();
        RefreshBluetooth();
        Logger.Log("完成刷新所有数据");
    }

    private void LoadNetworkAdapters()
    {
        NetworkInterfaces.Clear();

        try
        {
            Logger.Log("开始加载网络适配器");
            var adapters = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            
            foreach (var adapter in adapters)
            {
                // 检查是否为虚拟网卡
                bool isVirtual = IsVirtualAdapter(adapter);
                
                // 记录网卡详细信息
                Logger.Log($"网卡: '{adapter.Name}' | 描述: '{adapter.Description}' | 类型: {adapter.NetworkInterfaceType} | 状态: {adapter.OperationalStatus} | {(isVirtual ? "已过滤(虚拟网卡)" : "已添加(物理网卡)")}");
                
                // 过滤虚拟网卡，只保留物理网卡
                if (isVirtual)
                {
                    continue;
                }

                bool isWireless = adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
                bool isEthernet = adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                  adapter.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet;

                if (isEthernet)
                {
                    NetworkInterfaces.Add(new NetworkAdapterInfo
                    {
                        Id = adapter.Id,
                        Description = adapter.Name, // 使用实际网卡名称
                        Type = "Ethernet",
                        IsConnected = adapter.OperationalStatus == OperationalStatus.Up,
                        IsWireless = false
                    });
                    Logger.Log($"添加以太网适配器: {adapter.Name}");
                }
                else if (isWireless)
                {
                    WifiAdapterName = adapter.Name; // 使用实际无线网卡名称
                    Logger.Log($"设置无线网卡名称: {adapter.Name}");
                }
            }
            Logger.Log($"完成加载网络适配器: 以太网{NetworkInterfaces.Count}个, 无线网卡{(string.IsNullOrEmpty(WifiAdapterName) ? 0 : 1)}个");
        }
        catch (Exception ex)
        {
            Logger.LogException("加载网络适配器时发生错误", ex);
        }
    }

    /// <summary>
    /// 判断是否为虚拟网卡
    /// </summary>
    private bool IsVirtualAdapter(System.Net.NetworkInformation.NetworkInterface adapter)
    {
        string description = adapter.Description.ToLower();
        string name = adapter.Name.ToLower();
        
        // 检查描述和名称中是否包含虚拟网卡关键词
        string[] virtualKeywords = {
            "virtual", "vmware", "virtualbox", "hyper-v", "hyper v", 
            "vpn", "tunnel", "loopback", "p2p-group", "teredo",
            "microsoft network adapter multiplexor", "microsoft kernel debug",
            "veth", "vboxnet", "bridge", "vswitch", "wintun", "wireguard"
        };
        
        foreach (string keyword in virtualKeywords)
        {
            if (description.Contains(keyword) || name.Contains(keyword))
            {
                return true;
            }
        }
        
        // Loopback接口
        if (adapter.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
        {
            return true;
        }
        
        // Tunnel接口
        if (adapter.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel)
        {
            return true;
        }
        
        return false;
    }

    public void RefreshNetworks()
    {
        try
        {
            Logger.Log("开始刷新WiFi网络列表");
            var networks = _networkService.GetAvailableNetworks(true);
            var currentConnection = networks.FirstOrDefault(n => n.IsConnected);

            WpfApplication.Current.Dispatcher.Invoke(() =>
            {
                WiFiNetworks.Clear();
                foreach (var network in networks)
                {
                    WiFiNetworks.Add(network);
                    Logger.Log($"添加WiFi网络: {network.SSID}, 信号强度: {network.SignalQuality}%, 已连接: {network.IsConnected}");
                }

                if (currentConnection != null)
                {
                    IsWiFiConnected = true;
                    ConnectedSSID = currentConnection.SSID;
                    Logger.Log($"当前连接到: {currentConnection.SSID}");
                }
                else
                {
                    IsWiFiConnected = false;
                    ConnectedSSID = string.Empty;
                    Logger.Log("当前未连接到任何WiFi");
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogException("刷新WiFi网络时发生错误", ex);
        }
    }

    public void RefreshBluetooth()
    {
        try
        {
            Logger.Log("开始刷新蓝牙设备列表");
            var devices = _bluetoothService.GetDevices();
            var connectedDevices = devices.Where(d => d.IsConnected).ToList();

            WpfApplication.Current.Dispatcher.Invoke(() =>
            {
                BluetoothDevices.Clear();
                foreach (var device in devices)
                {
                    BluetoothDevices.Add(device);
                    Logger.Log($"添加蓝牙设备: {device.Name}, 已连接: {device.IsConnected}, 已配对: {device.IsPaired}");
                }

                IsBluetoothConnected = connectedDevices.Count > 0;
                Logger.Log($"蓝牙设备总数: {devices.Count}, 已连接设备: {connectedDevices.Count}");
            });
        }
        catch (Exception ex)
        {
            Logger.LogException("刷新蓝牙设备时发生错误", ex);
        }
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
            if (disposing)
            {
                _networkService.Dispose();
                _bluetoothService.Dispose();
                _brightnessService.Dispose();
                _audioService.Dispose();
            }
            _disposed = true;
        }
    }
}
