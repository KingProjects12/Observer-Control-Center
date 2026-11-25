using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Observer.ControlCenter.Services;

/// <summary>Best-effort native dark title bars for ordinary WPF dialog windows.</summary>
public static class DarkTitleBar
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            try
            {
                if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) return;
                nint hwnd = new WindowInteropHelper(window).Handle;
                int enabled = 1;
                int attr = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18985) ? 20 : 19;
                _ = DwmSetWindowAttribute(hwnd, attr, ref enabled, sizeof(int));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        };
    }
}
