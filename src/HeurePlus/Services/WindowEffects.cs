using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace HeurePlus.Services;

/// <summary>
/// Effets de fenêtre Windows 11 via DWM : barre de titre sombre, coins arrondis,
/// et (optionnel) matériau Mica. Sans effet — et sans erreur — sur les OS antérieurs.
/// </summary>
public static class WindowEffects
{
    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_MAINWINDOW = 2; // Mica

    public static void Apply(Window window, bool dark, bool mica = false)
    {
        try
        {
            IntPtr hwnd = new WindowInteropHelper(window).EnsureHandle();

            int darkValue = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkValue, sizeof(int));

            int corner = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

            if (mica)
            {
                int backdrop = DWMSBT_MAINWINDOW;
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }
        }
        catch
        {
            // DWM indisponible (Windows < 10 2004) : on ignore silencieusement.
        }
    }
}
