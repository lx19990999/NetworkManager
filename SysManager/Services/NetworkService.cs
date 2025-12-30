using System.Runtime.InteropServices;
using System.Text;
using SysManager.Native;
using static SysManager.Native.NativeMethods;

namespace SysManager.Services;

/// <summary>
/// WiFi network information model
/// </summary>
public class WiFiNetwork
{
    public string SSID { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public uint SignalQuality { get; set; }
    public bool IsSecured { get; set; }
    public bool IsConnected { get; set; }
    public bool HasProfile { get; set; }
    public DOT11_AUTH_ALGORITHM AuthAlgorithm { get; set; }
    public DOT11_CIPHER_ALGORITHM CipherAlgorithm { get; set; }

    public string SecurityType => AuthAlgorithm switch
    {
        DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_80211_OPEN => "Open",
        DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_80211_SHARED_KEY => "WEP",
        DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_WPA => "WPA",
        DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_WPA_PSK => "WPA-PSK",
        DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_RSNA => "WPA2",
        DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_RSNA_PSK => "WPA2-PSK",
        DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_WPA3 => "WPA3",
        DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_WPA3_SAE => "WPA3-SAE",
        _ => "Unknown"
    };

    public string SignalBars => SignalQuality switch
    {
        >= 80 => "████",
        >= 60 => "███░",
        >= 40 => "██░░",
        >= 20 => "█░░░",
        _ => "░░░░"
    };
}

/// <summary>
/// Network interface information model
/// </summary>
public class NetworkInterface
{
    public Guid InterfaceGuid { get; set; }
    public string Description { get; set; } = string.Empty;
    public WLAN_INTERFACE_STATE State { get; set; }

    public string StateDescription => State switch
    {
        WLAN_INTERFACE_STATE.wlan_interface_state_connected => "已连接",
        WLAN_INTERFACE_STATE.wlan_interface_state_disconnected => "已断开",
        WLAN_INTERFACE_STATE.wlan_interface_state_associating => "正在关联",
        WLAN_INTERFACE_STATE.wlan_interface_state_authenticating => "正在认证",
        WLAN_INTERFACE_STATE.wlan_interface_state_discovering => "正在发现",
        WLAN_INTERFACE_STATE.wlan_interface_state_disconnecting => "正在断开",
        _ => "未知状态"
    };
}

/// <summary>
/// WiFi network management service using Native WLAN API
/// </summary>
public class NetworkService : IDisposable
{
    private IntPtr _clientHandle = IntPtr.Zero;
    private bool _disposed;
    private Guid _currentInterfaceGuid;

    public event EventHandler<string>? NetworkStatusChanged;

    public NetworkService()
    {
        Initialize();
    }

    private void Initialize()
    {
        int result = WlanOpenHandle(WLAN_API_VERSION, IntPtr.Zero, out _, out _clientHandle);
        if (result != ERROR_SUCCESS)
        {
            throw new InvalidOperationException($"Failed to open WLAN handle. Error code: {result}");
        }
    }

    /// <summary>
    /// Get all wireless network interfaces
    /// </summary>
    public List<NetworkInterface> GetNetworkInterfaces()
    {
        var interfaces = new List<NetworkInterface>();

        int result = WlanEnumInterfaces(_clientHandle, IntPtr.Zero, out IntPtr pInterfaceList);
        if (result != ERROR_SUCCESS || pInterfaceList == IntPtr.Zero)
        {
            return interfaces;
        }

        try
        {
            var interfaceList = new WLAN_INTERFACE_INFO_LIST(pInterfaceList);
            foreach (var info in interfaceList.InterfaceInfo)
            {
                interfaces.Add(new NetworkInterface
                {
                    InterfaceGuid = info.InterfaceGuid,
                    Description = info.strInterfaceDescription,
                    State = info.isState
                });
            }

            if (interfaces.Count > 0)
            {
                _currentInterfaceGuid = interfaces[0].InterfaceGuid;
            }
        }
        finally
        {
            WlanFreeMemory(pInterfaceList);
        }

        return interfaces;
    }

    /// <summary>
    /// Scan and get available WiFi networks
    /// </summary>
    public List<WiFiNetwork> GetAvailableNetworks(bool forceRefresh = true)
    {
        var networks = new List<WiFiNetwork>();

        // Ensure we have a valid interface
        if (_currentInterfaceGuid == Guid.Empty)
        {
            var ifaces = GetNetworkInterfaces();
            if (ifaces.Count == 0) return networks;
        }

        var interfaceGuid = _currentInterfaceGuid;
        
        // Optionally force a refresh scan
        if (forceRefresh)
        {
            ForceNetworkScan(interfaceGuid);
        }
        
        int result = WlanGetAvailableNetworkList(_clientHandle, ref interfaceGuid, 0, IntPtr.Zero, out IntPtr pNetworkList);
        if (result != ERROR_SUCCESS || pNetworkList == IntPtr.Zero)
        {
            return networks;
        }

        try
        {
            uint numberOfItems = (uint)Marshal.ReadInt32(pNetworkList);
            int offset = 8; // Skip dwNumberOfItems and dwIndex

            for (uint i = 0; i < numberOfItems; i++)
            {
                IntPtr pNetwork = new IntPtr(pNetworkList.ToInt64() + offset);
                var network = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK>(pNetwork);

                string ssid = network.dot11Ssid.GetSSID();
                if (!string.IsNullOrEmpty(ssid))
                {
                    // Check if already added (same SSID)
                    var existing = networks.FirstOrDefault(n => n.SSID == ssid);
                    if (existing != null)
                    {
                        // Keep the one with better signal
                        if (network.wlanSignalQuality > existing.SignalQuality)
                        {
                            networks.Remove(existing);
                        }
                        else
                        {
                            offset += Marshal.SizeOf(typeof(WLAN_AVAILABLE_NETWORK));
                            continue;
                        }
                    }

                    networks.Add(new WiFiNetwork
                    {
                        SSID = ssid,
                        ProfileName = network.strProfileName,
                        SignalQuality = network.wlanSignalQuality,
                        IsSecured = network.bSecurityEnabled,
                        HasProfile = !string.IsNullOrEmpty(network.strProfileName),
                        IsConnected = (network.dwFlags & 1) != 0, // WLAN_AVAILABLE_NETWORK_CONNECTED
                        AuthAlgorithm = network.dot11DefaultAuthAlgorithm,
                        CipherAlgorithm = network.dot11DefaultCipherAlgorithm
                    });
                }

                offset += Marshal.SizeOf(typeof(WLAN_AVAILABLE_NETWORK));
            }
        }
        finally
        {
            WlanFreeMemory(pNetworkList);
        }

        return networks.OrderByDescending(n => n.IsConnected)
                       .ThenByDescending(n => n.SignalQuality)
                       .ToList();
    }

    /// <summary>
    /// Force a network scan by requesting a refresh
    /// </summary>
    private void ForceNetworkScan(Guid interfaceGuid)
    {
        try
        {
            // Create a dummy connection request to trigger a scan
            var connectionParams = new WLAN_CONNECTION_PARAMETERS
            {
                wlanConnectionMode = WLAN_CONNECTION_MODE.wlan_connection_mode_discovery_unsecure,
                strProfile = "", // Use empty string instead of null
                pDot11Ssid = IntPtr.Zero,
                pDesiredBssidList = IntPtr.Zero,
                dot11BssType = DOT11_BSS_TYPE.dot11_BSS_type_any,
                dwFlags = 0
            };

            // This may fail, which is expected, but it triggers a scan
            WlanConnect(_clientHandle, ref interfaceGuid, ref connectionParams, IntPtr.Zero);
            
            // Wait a bit for the scan to complete
            System.Threading.Thread.Sleep(500);
        }
        catch
        {
            // Ignore errors, as the scan is triggered anyway
        }
    }

    /// <summary>
    /// Get saved WiFi profiles
    /// </summary>
    public List<string> GetSavedProfiles()
    {
        var profiles = new List<string>();

        if (_currentInterfaceGuid == Guid.Empty)
        {
            var ifaces = GetNetworkInterfaces();
            if (ifaces.Count == 0) return profiles;
        }

        var interfaceGuid = _currentInterfaceGuid;
        int result = WlanGetProfileList(_clientHandle, ref interfaceGuid, IntPtr.Zero, out IntPtr pProfileList);
        if (result != ERROR_SUCCESS || pProfileList == IntPtr.Zero)
        {
            return profiles;
        }

        try
        {
            uint numberOfItems = (uint)Marshal.ReadInt32(pProfileList);
            int offset = 8;

            for (uint i = 0; i < numberOfItems; i++)
            {
                IntPtr pProfile = new IntPtr(pProfileList.ToInt64() + offset);
                var profile = Marshal.PtrToStructure<WLAN_PROFILE_INFO>(pProfile);
                profiles.Add(profile.strProfileName);
                offset += Marshal.SizeOf(typeof(WLAN_PROFILE_INFO));
            }
        }
        finally
        {
            WlanFreeMemory(pProfileList);
        }

        return profiles;
    }

    /// <summary>
    /// Connect to a WiFi network using saved profile
    /// </summary>
    public bool Connect(string profileName)
    {
        if (_currentInterfaceGuid == Guid.Empty)
        {
            var ifaces = GetNetworkInterfaces();
            if (ifaces.Count == 0) return false;
        }

        var interfaceGuid = _currentInterfaceGuid;
        var connectionParams = new WLAN_CONNECTION_PARAMETERS
        {
            wlanConnectionMode = WLAN_CONNECTION_MODE.wlan_connection_mode_profile,
            strProfile = profileName,
            pDot11Ssid = IntPtr.Zero,
            pDesiredBssidList = IntPtr.Zero,
            dot11BssType = DOT11_BSS_TYPE.dot11_BSS_type_any,
            dwFlags = 0
        };

        int result = WlanConnect(_clientHandle, ref interfaceGuid, ref connectionParams, IntPtr.Zero);
        if (result == ERROR_SUCCESS)
        {
            NetworkStatusChanged?.Invoke(this, $"正在连接到 {profileName}...");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Connect to a new WiFi network with password
    /// </summary>
    public bool ConnectWithPassword(string ssid, string password, DOT11_AUTH_ALGORITHM auth, DOT11_CIPHER_ALGORITHM cipher)
    {
        if (_currentInterfaceGuid == Guid.Empty)
        {
            var ifaces = GetNetworkInterfaces();
            if (ifaces.Count == 0) return false;
        }

        // Create profile XML
        string profileXml = GenerateProfileXml(ssid, password, auth, cipher);
        
        var interfaceGuid = _currentInterfaceGuid;
        
        // Set profile
        int result = WlanSetProfile(_clientHandle, ref interfaceGuid, 0, profileXml, null, true, IntPtr.Zero, out uint reasonCode);
        if (result != ERROR_SUCCESS)
        {
            return false;
        }

        // Connect using the new profile
        return Connect(ssid);
    }

    private string GenerateProfileXml(string ssid, string password, DOT11_AUTH_ALGORITHM auth, DOT11_CIPHER_ALGORITHM cipher)
    {
        string authStr = auth switch
        {
            DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_80211_OPEN => "open",
            DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_WPA_PSK => "WPAPSK",
            DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_RSNA_PSK => "WPA2PSK",
            DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_WPA3_SAE => "WPA3SAE",
            _ => "WPA2PSK"
        };

        string encStr = cipher switch
        {
            DOT11_CIPHER_ALGORITHM.DOT11_CIPHER_ALGO_NONE => "none",
            DOT11_CIPHER_ALGORITHM.DOT11_CIPHER_ALGO_WEP => "WEP",
            DOT11_CIPHER_ALGORITHM.DOT11_CIPHER_ALGO_TKIP => "TKIP",
            DOT11_CIPHER_ALGORITHM.DOT11_CIPHER_ALGO_CCMP => "AES",
            _ => "AES"
        };

        string ssidHex = BitConverter.ToString(Encoding.UTF8.GetBytes(ssid)).Replace("-", "");

        return $@"<?xml version=""1.0""?>
<WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
    <name>{ssid}</name>
    <SSIDConfig>
        <SSID>
            <hex>{ssidHex}</hex>
            <name>{ssid}</name>
        </SSID>
    </SSIDConfig>
    <connectionType>ESS</connectionType>
    <connectionMode>auto</connectionMode>
    <MSM>
        <security>
            <authEncryption>
                <authentication>{authStr}</authentication>
                <encryption>{encStr}</encryption>
                <useOneX>false</useOneX>
            </authEncryption>
            <sharedKey>
                <keyType>passPhrase</keyType>
                <protected>false</protected>
                <keyMaterial>{password}</keyMaterial>
            </sharedKey>
        </security>
    </MSM>
</WLANProfile>";
    }

    /// <summary>
    /// Disconnect from current WiFi network
    /// </summary>
    public bool Disconnect()
    {
        if (_currentInterfaceGuid == Guid.Empty)
        {
            var ifaces = GetNetworkInterfaces();
            if (ifaces.Count == 0) return false;
        }

        var interfaceGuid = _currentInterfaceGuid;
        int result = WlanDisconnect(_clientHandle, ref interfaceGuid, IntPtr.Zero);
        if (result == ERROR_SUCCESS)
        {
            NetworkStatusChanged?.Invoke(this, "已断开WiFi连接");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Delete a saved WiFi profile
    /// </summary>
    public bool DeleteProfile(string profileName)
    {
        if (_currentInterfaceGuid == Guid.Empty)
        {
            var ifaces = GetNetworkInterfaces();
            if (ifaces.Count == 0) return false;
        }

        var interfaceGuid = _currentInterfaceGuid;
        int result = WlanDeleteProfile(_clientHandle, ref interfaceGuid, profileName, IntPtr.Zero);
        return result == ERROR_SUCCESS;
    }

    /// <summary>
    /// Get currently connected network
    /// </summary>
    public WiFiNetwork? GetCurrentConnection()
    {
        return GetAvailableNetworks().FirstOrDefault(n => n.IsConnected);
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
            if (_clientHandle != IntPtr.Zero)
            {
                WlanCloseHandle(_clientHandle, IntPtr.Zero);
                _clientHandle = IntPtr.Zero;
            }
            _disposed = true;
        }
    }

    ~NetworkService()
    {
        Dispose(false);
    }
}
