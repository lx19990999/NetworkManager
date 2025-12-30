using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace SysManager.Helpers;

/// <summary>
/// 任务栏位置和状态信息
/// </summary>
public enum ABEdge
{
    ABE_LEFT = 0,
    ABE_TOP = 1,
    ABE_RIGHT = 2,
    ABE_BOTTOM = 3
}

[StructLayout(LayoutKind.Sequential)]
public struct APPBARDATA
{
    public int cbSize;
    public IntPtr hWnd;
    public int uCallbackMessage;
    public ABEdge uEdge;
    public RECT rc;
    public bool lParam;
}

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int left;
    public int top;
    public int right;
    public int bottom;
}

public static class TaskbarHelper
{
    private const string User32 = "user32.dll";
    
    [DllImport(User32, SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    
    [DllImport(User32, SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
    
    [DllImport(User32, SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);
    
    [DllImport(User32, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);
    
    private const uint SPI_GETWORKAREA = 0x0030;
    
    /// <summary>
    /// 获取任务栏位置
    /// </summary>
    public static TaskbarPosition GetTaskbarPosition()
    {
        // 获取工作区域（屏幕区域减去任务栏）
        RECT workArea = new RECT();
        SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0);
        
        // 获取屏幕区域
        var screenWidth = (int)SystemParameters.WorkArea.Width;
        var screenHeight = (int)SystemParameters.WorkArea.Height;
        
        // 计算任务栏位置
        if (workArea.left > 0)
            return TaskbarPosition.Left;
        else if (workArea.top > 0)
            return TaskbarPosition.Top;
        else if (workArea.right < screenWidth)
            return TaskbarPosition.Right;
        else if (workArea.bottom < screenHeight)
            return TaskbarPosition.Bottom;
        
        // 默认返回底部
        return TaskbarPosition.Bottom;
    }
    
    /// <summary>
    /// 获取任务栏尺寸
    /// </summary>
    public static System.Windows.Size GetTaskbarSize()
    {
        RECT workArea = new RECT();
        SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0);
        
        var screenWidth = (int)SystemParameters.WorkArea.Width;
        var screenHeight = (int)SystemParameters.WorkArea.Height;
        
        var position = GetTaskbarPosition();
        
        return position switch
        {
            TaskbarPosition.Left => new System.Windows.Size(workArea.left, screenHeight),
            TaskbarPosition.Top => new System.Windows.Size(screenWidth, workArea.top),
            TaskbarPosition.Right => new System.Windows.Size(screenWidth - workArea.right, screenHeight),
            TaskbarPosition.Bottom => new System.Windows.Size(screenWidth, screenHeight - workArea.bottom),
            _ => new System.Windows.Size(0, 0)
        };
    }
    
    /// <summary>
    /// 获取窗口应显示的位置
    /// </summary>
    public static System.Windows.Point GetWindowPosition(double windowWidth, double windowHeight)
    {
        var position = GetTaskbarPosition();
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        
        return position switch
        {
            TaskbarPosition.Left => new System.Windows.Point(0, screenHeight - windowHeight),
            TaskbarPosition.Top => new System.Windows.Point(screenWidth - windowWidth, 0),
            TaskbarPosition.Right => new System.Windows.Point(screenWidth - windowWidth, screenHeight - windowHeight),
            TaskbarPosition.Bottom => new System.Windows.Point(screenWidth - windowWidth, screenHeight - windowHeight),
            _ => new System.Windows.Point(screenWidth - windowWidth, screenHeight - windowHeight) // 默认底部
        };
    }
}

public enum TaskbarPosition
{
    Left,
    Top,
    Right,
    Bottom
}