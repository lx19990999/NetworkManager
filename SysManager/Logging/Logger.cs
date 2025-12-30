using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace SysManager.Logging;

public static class Logger
{
    private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
    private static readonly string LogFilePath = Path.Combine(LogDirectory, $"SysManager_{DateTime.Now:yyyyMMdd}.log");
    
    static Logger()
    {
        // 确保日志目录存在
        if (!Directory.Exists(LogDirectory))
        {
            Directory.CreateDirectory(LogDirectory);
        }
        
        // 清理7天前的日志文件
        CleanupOldLogs();
    }
    
    public static void Log(string message)
    {
        try
        {
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(LogFilePath, logMessage, Encoding.UTF8);
        }
        catch
        {
            // 如果日志写入失败，忽略错误
        }
    }
    
    public static void LogException(string message, Exception ex)
    {
        Log($"{message}: {ex.Message}{Environment.NewLine}StackTrace: {ex.StackTrace}");
    }
    
    private static void CleanupOldLogs()
    {
        try
        {
            var directoryInfo = new DirectoryInfo(LogDirectory);
            if (!directoryInfo.Exists) return;
            
            var cutoffDate = DateTime.Now.AddDays(-7);
            var oldLogFiles = directoryInfo.GetFiles("SysManager_*.log")
                                          .Where(f => f.CreationTime < cutoffDate);
            
            foreach (var file in oldLogFiles)
            {
                try
                {
                    file.Delete();
                }
                catch
                {
                    // 忽略删除失败的文件
                }
            }
        }
        catch
        {
            // 如果清理失败，忽略错误
        }
    }
}