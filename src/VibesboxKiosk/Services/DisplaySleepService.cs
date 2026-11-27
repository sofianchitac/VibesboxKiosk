using System;
using System.Runtime.InteropServices;

namespace VibesboxKiosk.Services;

internal static class DisplaySleepService
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private const uint WM_SYSCOMMAND   = 0x0112;
    private const int  SC_MONITORPOWER = 0xF170;
    private const uint INPUT_MOUSE      = 0;
    private const uint MOUSEEVENTF_MOVE = 0x0001;

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
    /// Turns the display back on by injecting a 1-pixel mouse move and its undo
    /// (net zero), the same input that wakes it by hand. SC_MONITORPOWER "on" is
    /// unreliable on current Windows. Harmless when the display is already on.
    /// </summary>
    public static void WakeDisplay()
    {
        var inputs = new[] { Move(1), Move(-1) };
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) != inputs.Length)
            Log.Error($"[Display] Wake failed: SendInput error {Marshal.GetLastWin32Error()}");
    }

    private static INPUT Move(int dx) => new()
    {
        type = INPUT_MOUSE,
        mi = new MOUSEINPUT { dx = dx, dwFlags = MOUSEEVENTF_MOVE },
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi; // the union's largest member, so the size is right
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
