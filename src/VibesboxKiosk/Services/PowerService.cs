using System;
using System.Diagnostics;

namespace VibesboxKiosk.Services;

public static class PowerService
{
    public static void Shutdown() => Run("/s /t 0");
    public static void Restart()  => Run("/r /t 0");

    private static void Run(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName        = Path.Combine(Environment.SystemDirectory, "shutdown.exe"),
                Arguments       = args,
                UseShellExecute = false,
                CreateNoWindow  = true,
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log.Error($"[PowerService] shutdown.exe {args} failed: {ex.Message}");
        }
    }
}
