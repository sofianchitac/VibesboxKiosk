using System;
using System.IO;

namespace VibesboxKiosk.Services;

/// <summary>
/// Append-only file logger for the kiosk. Writes to
/// %LOCALAPPDATA%\VibesboxKiosk\logs\kiosk-YYYY-MM-DD.log
/// (one file per day). Thread-safe; AOT-compatible (no reflection).
///
/// WinUI 3 GUI-subsystem binaries have no console attached when launched by
/// Task Scheduler at logon, so Console.WriteLine and Debug.WriteLine both go
/// nowhere visible in production. This file sink is the only source of truth
/// for post-mortem diagnostics.
///
/// Logging never throws — disk errors are silently dropped to keep the kiosk alive.
/// </summary>
public static class Log
{
    private const int RetentionDays = 7;

    private static readonly object _lock = new();
    private static readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VibesboxKiosk", "logs");

    private static DateTime _lastPruneDate = DateTime.MinValue;

    public static string Directory => _dir;

    public static void Info(string message)  => Write("INFO",  message);
    public static void Warn(string message)  => Write("WARN",  message);
    public static void Error(string message) => Write("ERROR", message);

    public static void Error(string message, Exception ex)
        => Write("ERROR", $"{message}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");

    private static void Write(string level, string message)
    {
        var now = DateTime.Now;
        var line = $"{now:yyyy-MM-dd HH:mm:ss.fff} [{level,-5}] {message}{Environment.NewLine}";

        // Keep emitting to OutputDebugString so DbgView still works during dev.
        System.Diagnostics.Debug.Write(line);

        try
        {
            lock (_lock)
            {
                System.IO.Directory.CreateDirectory(_dir);

                // Prune at most once per calendar day (cheap when no-op, and
                // logging volume is low enough that holding the lock is fine).
                if (now.Date != _lastPruneDate)
                {
                    _lastPruneDate = now.Date;
                    PruneOldLogs(now);
                }

                File.AppendAllText(Path.Combine(_dir, $"kiosk-{now:yyyy-MM-dd}.log"), line);
            }
        }
        catch
        {
            // Logging must never throw.
        }
    }

    private static void PruneOldLogs(DateTime now)
    {
        try
        {
            var cutoff = now.AddDays(-RetentionDays);
            foreach (var file in System.IO.Directory.EnumerateFiles(_dir, "kiosk-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch
        {
            // A failed prune must not affect the pending write.
        }
    }
}
