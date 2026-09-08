using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace EolTester.App.Interop;

/// <summary>
/// Khóa tỉ lệ khung hình khi người dùng kéo viền cửa sổ bằng chuột (WM_SIZING),
/// để nội dung luôn scale đồng bộ theo Viewbox thay vì vỡ layout. Không can thiệp Maximize
/// (Windows không gửi WM_SIZING khi Maximize) — đúng ý đồ để Maximize được lấp đầy tự do.
/// </summary>
public static class WindowAspectRatioLock
{
    private const int WM_SIZING = 0x0214;
    private const int WMSZ_LEFT = 1;
    private const int WMSZ_RIGHT = 2;
    private const int WMSZ_TOP = 3;
    private const int WMSZ_TOPLEFT = 4;
    private const int WMSZ_TOPRIGHT = 5;
    private const int WMSZ_BOTTOM = 6;
    private const int WMSZ_BOTTOMLEFT = 7;
    private const int WMSZ_BOTTOMRIGHT = 8;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static void Attach(Window window, double aspectRatio)
    {
        window.SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(window) is not HwndSource hwndSource) return;
            hwndSource.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                WndProc(msg, wParam, lParam, ref handled, aspectRatio));
        };
    }

    private static IntPtr WndProc(int msg, IntPtr wParam, IntPtr lParam, ref bool handled, double aspectRatio)
    {
        if (msg != WM_SIZING) return IntPtr.Zero;

        var rc = Marshal.PtrToStructure<RECT>(lParam);
        var width = rc.Right - rc.Left;
        var height = rc.Bottom - rc.Top;

        switch (wParam.ToInt32())
        {
            case WMSZ_LEFT:
            case WMSZ_RIGHT:
                rc.Bottom = rc.Top + (int)Math.Round(width / aspectRatio);
                break;
            case WMSZ_TOP:
            case WMSZ_BOTTOM:
                rc.Right = rc.Left + (int)Math.Round(height * aspectRatio);
                break;
            case WMSZ_TOPLEFT:
                rc.Left = rc.Right - (int)Math.Round(height * aspectRatio);
                break;
            case WMSZ_TOPRIGHT:
                rc.Right = rc.Left + (int)Math.Round(height * aspectRatio);
                break;
            case WMSZ_BOTTOMLEFT:
                rc.Bottom = rc.Top + (int)Math.Round(width / aspectRatio);
                break;
            case WMSZ_BOTTOMRIGHT:
            default:
                rc.Bottom = rc.Top + (int)Math.Round(width / aspectRatio);
                break;
        }

        Marshal.StructureToPtr(rc, lParam, true);
        handled = true;
        return IntPtr.Zero;
    }
}
