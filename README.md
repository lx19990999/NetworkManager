# NetworkManager

一个Windows系统托盘网络管理工具，具有Windows 7 Aero风格的界面，支持WiFi/蓝牙管理、亮度/音量控制等功能。

## 功能特性

- **系统托盘驻留** - 轻松访问网络管理功能
- **WiFi管理** - 扫描、连接和断开WiFi网络
- **蓝牙管理** - 发现和连接蓝牙设备
- **网络适配器管理** - 管理以太网和无线网卡
- **Aero玻璃效果** - Windows 7风格的透明界面
- **智能定位** - 窗口根据任务栏位置智能显示
- **日志记录** - 自动按日期生成日志并清理旧日志

## 系统要求

- Windows 10/11
- .NET 8.0 运行时

## 使用方法

1. 下载最新发布的可执行文件
2. 运行 `SysManager.exe`
3. 点击系统托盘图标打开管理界面

## 构建说明

```bash
# 克隆项目
git clone https://github.com/lx19990999/NetworkManager.git
cd NetworkManager

# 恢复依赖
dotnet restore SysManager/SysManager.csproj

# 构建项目
dotnet build SysManager/SysManager.csproj --configuration Release

# 发布单文件应用
dotnet publish SysManager/SysManager.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true
```

## 自动构建

项目配置了GitHub Actions，当推送到main分支或创建新标签时会自动构建和发布。

## 许可证

MIT License