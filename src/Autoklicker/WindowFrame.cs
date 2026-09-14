using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Autoklicker;

internal static class WindowFrame
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    internal static void Apply(Window window)
    {
        nint hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == 0) return;
        // The compositor owns the window silhouette, not a rounded rectangle
        // drawn inside an otherwise rectangular transparent window.
        int rounded = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(hwnd, 33, ref rounded, sizeof(int));
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        int noBorder = -2; // DWMWA_COLOR_NONE: no gray system outline
        DwmSetWindowAttribute(hwnd, 34, ref noBorder, sizeof(int));
    }
}
