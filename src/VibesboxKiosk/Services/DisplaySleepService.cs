using System;
using System.Runtime.InteropServices;

namespace VibesboxKiosk.Services;

internal static class DisplaySleepService
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_SYSCOMMAND   = 0x0112;
    private const int  SC_MONITORPOWER = 0xF170;

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
}
