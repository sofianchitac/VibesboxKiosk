using System;
using System.Runtime.InteropServices;

namespace VibesboxKiosk.Services;

internal static class DisplaySleepService
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private const uint WM_SYSCOMMAND   = 0x0112;
    private const int  SC_MONITORPOWER = 0xF170;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;

    // HWND_BROADCAST (-1) delivers the message to all top-level windows
    private static readonly IntPtr HwndBroadcast = new(-1);

    /// <summary>
    /// Sends the OS "turn off display" signal via WM_SYSCOMMAND / SC_MONITORPOWER.
    /// The screen wakes on any hardware input (touch, keyboard, mouse).
    /// </summary>
    public static void TurnOffDisplay()
    {
        PostMessage(HwndBroadcast, WM_SYSCOMMAND, new IntPtr(SC_MONITORPOWER), new IntPtr(2));
    }

    /// <summary>
    /// Turns the display back on. A one-shot ES_DISPLAY_REQUIRED does it; injected
    /// mouse/keyboard input (SendInput) does not wake the display, and
    /// SC_MONITORPOWER "on" lights it for only a second (both tested on Windows 11
    /// 24H2). Harmless when the display is already on.
    /// </summary>
    public static void WakeDisplay()
    {
        if (SetThreadExecutionState(ES_DISPLAY_REQUIRED) == 0)
            Log.Error("[Display] Wake failed: SetThreadExecutionState returned 0");
    }
}
