using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SysPilot.Uninstaller;

internal static partial class Program
{
    // State
    static nint _hWnd, _gdipToken, _titleFont, _textFont, _smallFont, _fontFamily, _stringFormat;
    static bool _closeHover, _btnHover, _closePressed, _btnPressed;
    static HitRect _closeRect, _btnRect;
    static int _state; // 0=confirm, 1=running, 2=done
    static float _progress;
    static string _status = "Ready to uninstall";
    static string _installDir = "";
    static bool _quietMode;

    static readonly Native.WndProc _wndProcDelegate = WndProc;

    record struct HitRect(int X, int Y, int W, int H) { public bool Contains(int px, int py) => px >= X && px < X + W && py >= Y && py < Y + H; }

    // Theme
    const uint BG = 0xFF1A1A1A, Surface = 0xFF252525, Accent = 0xFF007ACC, AccentHover = 0xFF1E90FF;
    const uint TextColor = 0xFFE0E0E0, TextMuted = 0xFF808080, Success = 0xFF4CAF50, Error = 0xFFE81123, ProgressBg = 0xFF2A2A2A;
    const int WinW = 400, WinH = 280, Padding = 24;

    [STAThread]
    static void Main(string[] args)
    {
        Native.CoInitializeEx(0, 2); // COINIT_APARTMENTTHREADED

        _installDir = Path.GetDirectoryName(Environment.ProcessPath) ?? "";

        // Check for quiet mode
        _quietMode = args.Any(a => a.Equals("-quiet", StringComparison.OrdinalIgnoreCase) ||
                                    a.Equals("/quiet", StringComparison.OrdinalIgnoreCase));

        if (_quietMode)
        {
            RunUninstall();
            return;
        }

        // GUI mode
        var input = Native.GdiplusStartupInput.Default;
        Native.GdiplusStartup(out _gdipToken, ref input, out _);
        CreateFonts();

        var hInstance = Native.GetModuleHandleW(0);
        var className = Marshal.StringToHGlobalUni("SysPilotUninstall\0");
        var wc = new Native.WNDCLASSEX {
            cbSize = (uint)Marshal.SizeOf<Native.WNDCLASSEX>(),
            style = 0x0003,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = hInstance,
            hCursor = Native.LoadCursorW(0, 32512),
            lpszClassName = className
        };
        Native.RegisterClassExW(ref wc);

        int x = (Native.GetSystemMetrics(0) - WinW) / 2;
        int y = (Native.GetSystemMetrics(1) - WinH) / 2;
        var title = Marshal.StringToHGlobalUni("SysPilot Uninstall\0");
        _hWnd = Native.CreateWindowExW(0x40000, className, title, unchecked((int)0x80000000) | 0x10000000 | 0x80000 | 0x20000, x, y, WinW, WinH, 0, 0, hInstance, 0);
        Marshal.FreeHGlobal(title);

        int dark = 1; Native.DwmSetWindowAttribute(_hWnd, 20, ref dark, 4);
        int round = 2; Native.DwmSetWindowAttribute(_hWnd, 33, ref round, 4);

        Native.ShowWindow(_hWnd, 5);
        Native.UpdateWindow(_hWnd);

        while (Native.GetMessageW(out var msg, 0, 0, 0)) { Native.TranslateMessage(ref msg); Native.DispatchMessageW(ref msg); }

        Native.GdipDeleteFont(_titleFont); Native.GdipDeleteFont(_textFont); Native.GdipDeleteFont(_smallFont);
        Native.GdipDeleteFontFamily(_fontFamily); Native.GdipDeleteStringFormat(_stringFormat);
        Native.GdiplusShutdown(_gdipToken);
    }

    static void CreateFonts()
    {
        Native.GdipCreateFontFamilyFromName("Segoe UI", 0, out _fontFamily);
        Native.GdipCreateFont(_fontFamily, 22, 0, 2, out _titleFont);
        Native.GdipCreateFont(_fontFamily, 13, 0, 2, out _textFont);
        Native.GdipCreateFont(_fontFamily, 11, 0, 2, out _smallFont);
        Native.GdipCreateStringFormat(0, 0, out _stringFormat);
        Native.GdipSetStringFormatAlign(_stringFormat, 1);
        Native.GdipSetStringFormatLineAlign(_stringFormat, 1);
    }

    static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case 0x0F: OnPaint(hwnd); return 0; // WM_PAINT
            case 0x14: return 1; // WM_ERASEBKGND
            case 0x201: OnMouseDown(hwnd, Lo(lParam), Hi(lParam)); return 0; // WM_LBUTTONDOWN
            case 0x202: OnMouseUp(hwnd, Lo(lParam), Hi(lParam)); return 0; // WM_LBUTTONUP
            case 0x200: OnMouseMove(hwnd, Lo(lParam), Hi(lParam)); return 0; // WM_MOUSEMOVE
            case 0x20: // WM_SETCURSOR
                if (Lo(lParam) == 1) { Native.SetCursor(Native.LoadCursorW(0, (_closeHover || _btnHover) ? 32649 : 32512)); return 1; }
                break;
            case 0x113: // WM_TIMER
                if (_state == 1)
                {
                    Native.InvalidateRect(hwnd, 0, false);
                }
                return 0;
            case 0x02: Native.PostQuitMessage(0); return 0; // WM_DESTROY
        }
        return Native.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    static void OnPaint(nint hwnd)
    {
        Native.BeginPaint(hwnd, out var ps);
        Native.GetClientRect(hwnd, out var cr);
        int w = cr.Width, h = cr.Height;

        var memDC = Native.CreateCompatibleDC(ps.hdc);
        var memBmp = Native.CreateCompatibleBitmap(ps.hdc, w, h);
        var oldBmp = Native.SelectObject(memDC, memBmp);

        Native.GdipCreateFromHDC(memDC, out var g);
        Native.GdipSetSmoothingMode(g, 4);
        Native.GdipSetTextRenderingHint(g, 5);
        Native.GdipGraphicsClear(g, BG);

        // Close button
        _closeRect = new HitRect(w - 36, 8, 28, 28);
        if (_closeHover)
        {
            DrawRoundedRect(g, _closeRect.X, _closeRect.Y, _closeRect.W, _closeRect.H, 6, Error);
        }
        DrawX(g, _closeRect.X + 14, _closeRect.Y + 14);

        int cy = 50;
        DrawText(g, "SysPilot", _titleFont, TextColor, 0, cy, w, 30); cy += 40;

        string sub = _state switch { 0 => "Uninstall?", 1 => _status, 2 => "Uninstalled!", _ => "" };
        uint subColor = _state == 2 ? Success : TextMuted;
        DrawText(g, sub, _textFont, subColor, 0, cy, w, 24); cy += 40;

        // Progress bar
        int px = Padding, pw = w - Padding * 2, ph = 6;
        DrawRoundedRect(g, px, cy, pw, ph, 3, ProgressBg);
        if (_progress > 0)
        {
            int fillW = Math.Max(6, (int)(pw * _progress));
            DrawRoundedRect(g, px, cy, fillW, ph, 3, Accent);
        }
        cy += 30;

        // Path
        DrawText(g, TruncatePath(_installDir, 45), _smallFont, TextMuted, 0, cy, w, 20); cy += 40;

        // Button
        int btnW = 140, btnH = 40;
        _btnRect = new HitRect(w / 2 - btnW / 2, cy, btnW, btnH);
        string btnText = _state switch { 0 => "Uninstall", 1 => "Uninstalling...", 2 => "Close", _ => "" };
        bool enabled = _state != 1;
        uint btnBg = enabled ? (_btnHover ? AccentHover : Accent) : Surface;
        DrawRoundedRect(g, _btnRect.X, _btnRect.Y, _btnRect.W, _btnRect.H, 8, btnBg);
        DrawText(g, btnText, _textFont, TextColor, _btnRect.X, _btnRect.Y, _btnRect.W, _btnRect.H);

        Native.GdipDeleteGraphics(g);
        Native.BitBlt(ps.hdc, 0, 0, w, h, memDC, 0, 0, 0x00CC0020);
        Native.SelectObject(memDC, oldBmp);
        Native.DeleteObject(memBmp);
        Native.DeleteDC(memDC);
        Native.EndPaint(hwnd, ref ps);
    }

    static void DrawRoundedRect(nint g, int x, int y, int w, int h, int r, uint color)
    {
        Native.GdipCreatePath(0, out var path);
        float fx = x, fy = y, fw = w, fh = h, fr = r;
        Native.GdipAddPathArc(path, fx, fy, fr * 2, fr * 2, 180, 90);
        Native.GdipAddPathArc(path, fx + fw - fr * 2, fy, fr * 2, fr * 2, 270, 90);
        Native.GdipAddPathArc(path, fx + fw - fr * 2, fy + fh - fr * 2, fr * 2, fr * 2, 0, 90);
        Native.GdipAddPathArc(path, fx, fy + fh - fr * 2, fr * 2, fr * 2, 90, 90);
        Native.GdipClosePathFigure(path);
        Native.GdipCreateSolidFill(color, out var brush);
        Native.GdipFillPath(g, brush, path);
        Native.GdipDeleteBrush(brush);
        Native.GdipDeletePath(path);
    }

    static void DrawText(nint g, string text, nint font, uint color, int x, int y, int w, int h)
    {
        var rect = new Native.RectF(x, y, w, h);
        Native.GdipCreateSolidFill(color, out var brush);
        Native.GdipDrawString(g, text, text.Length, font, ref rect, _stringFormat, brush);
        Native.GdipDeleteBrush(brush);
    }

    static void DrawX(nint g, float cx, float cy)
    {
        Native.GdipCreatePen1(TextColor, 2, 2, out var pen);
        Native.GdipDrawLine(g, pen, cx - 5, cy - 5, cx + 5, cy + 5);
        Native.GdipDrawLine(g, pen, cx + 5, cy - 5, cx - 5, cy + 5);
        Native.GdipDeletePen(pen);
    }

    static void OnMouseDown(nint hwnd, int x, int y)
    {
        _closePressed = _closeRect.Contains(x, y);
        _btnPressed = _btnRect.Contains(x, y) && _state != 1;
        if (!_closePressed && !_btnPressed) { Native.ReleaseCapture(); Native.SendMessageW(hwnd, 0xA1, 2, 0); }
    }

    static void OnMouseUp(nint hwnd, int x, int y)
    {
        if (_closePressed && _closeRect.Contains(x, y))
        {
            Native.DestroyWindow(hwnd);
        }
        else if (_btnPressed && _btnRect.Contains(x, y))
        {
            if (_state == 0)
            {
                StartUninstallGUI(hwnd);
            }
            else if (_state == 2)
            {
                Native.DestroyWindow(hwnd);
            }
        }
        _closePressed = _btnPressed = false;
    }

    static void OnMouseMove(nint hwnd, int x, int y)
    {
        bool newClose = _closeRect.Contains(x, y);
        bool newBtn = _btnRect.Contains(x, y);
        if (newClose != _closeHover || newBtn != _btnHover) { _closeHover = newClose; _btnHover = newBtn; Native.InvalidateRect(hwnd, 0, false); }
    }

    static void StartUninstallGUI(nint hwnd)
    {
        _state = 1;
        _progress = 0;
        Native.InvalidateRect(hwnd, 0, false);
        Native.SetTimer(hwnd, 1, 50, 0);

        var h = hwnd;
        new Thread(() => {
            try
            {
                RunUninstallCore();
                _progress = 1; _state = 2; _status = "Complete!";
            }
            catch { _state = 2; _status = "Error"; }
            finally { Native.KillTimer(h, 1); Native.InvalidateRect(h, 0, false); }
        }).Start();
    }

    static void RunUninstall()
    {
        try { RunUninstallCore(); }
        catch { /* silent */ }
    }

    static void RunUninstallCore()
    {
        _progress = 0.1f; _status = "Stopping SysPilot...";
        try { foreach (var p in Process.GetProcessesByName("SysPilot")) { p.Kill(); p.WaitForExit(3000); } } catch { }

        _progress = 0.3f; _status = "Removing shortcuts...";
        Guid desktop = new("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");
        Guid programs = new("A77F5D77-2E2B-44C3-A6A2-ABA601054A51");
        Guid commonProgs = new("0139D44E-6AFE-49F2-8690-3DAFCAE6FFB8");
        DeleteFile(desktop, "SysPilot.lnk");
        DeleteFolder(commonProgs, "SysPilot");
        DeleteFolder(programs, "SysPilot");

        _progress = 0.5f; _status = "Removing registry...";
        try { Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\SysPilot", false); } catch { }

        _progress = 0.8f; _status = "Scheduling file removal...";
        var batch = Path.Combine(Path.GetTempPath(), "syspilot_uninstall.cmd");
        File.WriteAllText(batch, $"@echo off\r\nping 127.0.0.1 -n 3 >nul\r\nrmdir /s /q \"{_installDir}\"\r\ndel /f \"%~f0\"");
        Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = $"/c \"{batch}\"", WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true });
    }

    static void DeleteFile(Guid folder, string name)
    {
        try
        {
            var p = GetKnownPath(folder);
            if (p is not null)
            {
                var path = Path.Combine(p, name);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
        catch { }
    }

    static void DeleteFolder(Guid folder, string name)
    {
        try
        {
            var p = GetKnownPath(folder);
            if (p is not null)
            {
                var path = Path.Combine(p, name);
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
        }
        catch { }
    }

    static string? GetKnownPath(Guid id)
    {
        if (Native.SHGetKnownFolderPath(ref id, 0, 0, out var p) != 0)
        {
            return null;
        }
        try { return Marshal.PtrToStringUni(p); }
        finally { Marshal.FreeCoTaskMem(p); }
    }

    static string TruncatePath(string p, int max) => p.Length <= max ? p : "..." + p[(p.Length - max + 3)..];
    static int Lo(nint v) => (int)(v.ToInt64() & 0xFFFF);
    static int Hi(nint v) => (int)((v.ToInt64() >> 16) & 0xFFFF);
}

// Native P/Invoke with LibraryImport (NativeAOT compatible)
internal static partial class Native
{
    public delegate nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct WNDCLASSEX { public uint cbSize; public uint style; public nint lpfnWndProc; public int cbClsExtra; public int cbWndExtra; public nint hInstance; public nint hIcon; public nint hCursor; public nint hbrBackground; public nint lpszMenuName; public nint lpszClassName; public nint hIconSm; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG { public nint hwnd; public uint message; public nint wParam; public nint lParam; public uint time; public POINT pt; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PAINTSTRUCT { public nint hdc; public bool fErase; public RECT rcPaint; public bool fRestore; public bool fIncUpdate; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[]? rgbReserved; }

    [StructLayout(LayoutKind.Sequential)]
    public struct GdiplusStartupInput { public uint GdiplusVersion; public nint DebugEventCallback; public int SuppressBackgroundThread; public int SuppressExternalCodecs; public static GdiplusStartupInput Default => new() { GdiplusVersion = 1 }; }

    [StructLayout(LayoutKind.Sequential)]
    public struct GdiplusStartupOutput { public nint NotificationHook; public nint NotificationUnhook; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RectF { public float X, Y, Width, Height; public RectF(float x, float y, float w, float h) { X = x; Y = y; Width = w; Height = h; } }

    // User32
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial ushort RegisterClassExW(ref WNDCLASSEX lpwcx);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nint CreateWindowExW(int dwExStyle, nint lpClassName, nint lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UpdateWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TranslateMessage(ref MSG lpMsg);

    [LibraryImport("user32.dll")]
    public static partial nint DispatchMessageW(ref MSG lpMsg);

    [LibraryImport("user32.dll")]
    public static partial void PostQuitMessage(int nExitCode);

    [LibraryImport("user32.dll")]
    public static partial nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    public static extern nint BeginPaint(nint hWnd, out PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EndPaint(nint hWnd, ref PAINTSTRUCT lpPaint);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool InvalidateRect(nint hWnd, nint lpRect, [MarshalAs(UnmanagedType.Bool)] bool bErase);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetClientRect(nint hWnd, out RECT lpRect);

    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetrics(int nIndex);

    [LibraryImport("user32.dll")]
    public static partial nint LoadCursorW(nint hInstance, int lpCursorName);

    [LibraryImport("user32.dll")]
    public static partial nint SetCursor(nint hCursor);

    [LibraryImport("user32.dll")]
    public static partial nint SetTimer(nint hWnd, nint nIDEvent, uint uElapse, nint lpTimerFunc);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool KillTimer(nint hWnd, nint uIDEvent);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ReleaseCapture();

    [LibraryImport("user32.dll")]
    public static partial nint SendMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll")]
    public static partial nint GetModuleHandleW(nint lpModuleName);

    // Gdi32
    [LibraryImport("gdi32.dll")]
    public static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateCompatibleBitmap(nint hdc, int cx, int cy);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint hdc, nint h);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint ho);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BitBlt(nint hdc, int x, int y, int cx, int cy, nint hdcSrc, int x1, int y1, int rop);

    // GdiPlus
    [LibraryImport("gdiplus.dll")]
    public static partial int GdiplusStartup(out nint token, ref GdiplusStartupInput input, out GdiplusStartupOutput output);

    [LibraryImport("gdiplus.dll")]
    public static partial void GdiplusShutdown(nint token);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipCreateFromHDC(nint hdc, out nint graphics);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDeleteGraphics(nint graphics);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipSetSmoothingMode(nint graphics, int smoothingMode);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipSetTextRenderingHint(nint graphics, int textRenderingHint);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipGraphicsClear(nint graphics, uint color);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipCreateSolidFill(uint color, out nint brush);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDeleteBrush(nint brush);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipCreatePen1(uint color, float width, int unit, out nint pen);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDeletePen(nint pen);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipCreatePath(int fillMode, out nint path);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDeletePath(nint path);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipAddPathArc(nint path, float x, float y, float width, float height, float startAngle, float sweepAngle);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipClosePathFigure(nint path);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipFillPath(nint graphics, nint brush, nint path);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipCreateFontFamilyFromName([MarshalAs(UnmanagedType.LPWStr)] string name, nint fontCollection, out nint fontFamily);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDeleteFontFamily(nint fontFamily);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipCreateFont(nint fontFamily, float emSize, int style, int unit, out nint font);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDeleteFont(nint font);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipCreateStringFormat(int formatAttributes, int language, out nint format);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDeleteStringFormat(nint format);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipSetStringFormatAlign(nint format, int align);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipSetStringFormatLineAlign(nint format, int align);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDrawString(nint graphics, [MarshalAs(UnmanagedType.LPWStr)] string str, int length, nint font, ref RectF layoutRect, nint stringFormat, nint brush);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDrawLine(nint graphics, nint pen, float x1, float y1, float x2, float y2);

    // Dwmapi
    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(nint hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    // Shell32
    [LibraryImport("shell32.dll")]
    public static partial int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, nint hToken, out nint ppszPath);

    // Ole32
    [LibraryImport("ole32.dll")]
    public static partial int CoInitializeEx(nint pvReserved, uint dwCoInit);
}
