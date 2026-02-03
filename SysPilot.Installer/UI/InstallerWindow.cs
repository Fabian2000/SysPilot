using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using SysPilot.Installer.Native;
using SysPilot.Installer.Install;

namespace SysPilot.Installer.UI;

internal sealed class InstallerWindow : IDisposable
{
    private readonly nint _hInstance;
    private nint _className;
    private readonly User32.WndProc _wndProc;
    private nint _hWnd;
    private nint _gdipToken;

    // UI State
    private string _installPath;
    private InstallState _state = InstallState.Ready;
    private float _progress = 0f;
    private string _statusText = "Ready to install";
    private string? _errorMessage;

    // Hit testing regions
    private Rect _closeButtonRect;
    private Rect _installButtonRect;
    private Rect _browseButtonRect;
    private bool _closeHover;
    private bool _installHover;
    private bool _browseHover;

    // Mouse button tracking for proper click behavior
    private bool _closePressed;
    private bool _installPressed;
    private bool _browsePressed;

    // Checkbox tracking
    private Rect _checkStartMenuAllRect;
    private Rect _checkStartMenuUserRect;
    private Rect _checkDesktopRect;
    private bool _checkStartMenuAllHover;
    private bool _checkStartMenuUserHover;
    private bool _checkDesktopHover;
    private bool _checkStartMenuAllPressed;
    private bool _checkStartMenuUserPressed;
    private bool _checkDesktopPressed;

    // Shortcut options
    private bool _createStartMenuAll = true;      // All users (default)
    private bool _createStartMenuUser = false;    // Current user
    private bool _createDesktopShortcut = true;

    // Uninstall mode
    private bool _isUninstallMode;
    private string? _existingInstallDir;

    // Timer for progress animation
    private const nint TIMER_ID = 1;

    // Fonts (cached)
    private nint _titleFont;
    private nint _textFont;
    private nint _smallFont;
    private nint _fontFamily;
    private nint _stringFormat;
    private nint _stringFormatLeft;

    // Logo
    private nint _logo;

    public enum InstallState { Ready, Installing, Complete, Error, ConfirmUninstall, Uninstalling }

    private record struct Rect(int X, int Y, int Width, int Height)
    {
        public bool Contains(int px, int py) => px >= X && px < X + Width && py >= Y && py < Y + Height;
    }

    public InstallerWindow(string? existingInstallDir = null)
    {
        _installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SysPilot");
        _hInstance = User32.GetModuleHandleW(0);
        _wndProc = WndProc;

        // Set uninstall mode if already installed
        if (!string.IsNullOrEmpty(existingInstallDir))
        {
            _isUninstallMode = true;
            _existingInstallDir = existingInstallDir;
            _installPath = existingInstallDir;
            _state = InstallState.ConfirmUninstall;
            _statusText = "Ready to uninstall";
        }

        InitGdiPlus();
        CreateFonts();
        LoadLogo();
    }

    private void InitGdiPlus()
    {
        var input = GdiPlus.GdiplusStartupInput.Default;
        GdiPlus.GdiplusStartup(out _gdipToken, ref input, out _);
    }

    private void CreateFonts()
    {
        GdiPlus.GdipCreateFontFamilyFromName("Segoe UI", 0, out _fontFamily);
        if (_fontFamily == 0)
        {
            // Fallback to Arial
            GdiPlus.GdipCreateFontFamilyFromName("Arial", 0, out _fontFamily);
        }

        GdiPlus.GdipCreateFont(_fontFamily, Theme.TitleSize, 0, GdiPlus.UnitPixel, out _titleFont);
        GdiPlus.GdipCreateFont(_fontFamily, Theme.SubtitleSize, 0, GdiPlus.UnitPixel, out _textFont);
        GdiPlus.GdipCreateFont(_fontFamily, Theme.SmallSize, 0, GdiPlus.UnitPixel, out _smallFont);

        GdiPlus.GdipCreateStringFormat(0, 0, out _stringFormat);
        GdiPlus.GdipSetStringFormatAlign(_stringFormat, GdiPlus.StringAlignmentCenter);
        GdiPlus.GdipSetStringFormatLineAlign(_stringFormat, GdiPlus.StringAlignmentCenter);

        GdiPlus.GdipCreateStringFormat(0, 0, out _stringFormatLeft);
        GdiPlus.GdipSetStringFormatLineAlign(_stringFormatLeft, GdiPlus.StringAlignmentCenter);
    }

    private string? _tempLogoPath;

    private void LoadLogo()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("icon.png");

            if (stream is not null)
            {
                // Extract to temp file, then load with GDI+
                _tempLogoPath = Path.Combine(Path.GetTempPath(), $"syspilot_logo_{Environment.ProcessId}.png");
                using (var fileStream = File.Create(_tempLogoPath))
                {
                    stream.CopyTo(fileStream);
                }

                GdiPlus.GdipLoadImageFromFile(_tempLogoPath, out _logo);
            }
        }
        catch
        {
            // Logo is optional, continue without it
            _logo = 0;
        }
    }

    private void CleanupTempLogo()
    {
        try
        {
            if (_tempLogoPath is not null && File.Exists(_tempLogoPath))
            {
                File.Delete(_tempLogoPath);
            }
        }
        catch { }
    }

    public void Show()
    {
        // Register window class
        var className = "SysPilotInstaller\0";
        _className = Marshal.StringToHGlobalUni(className);

        var wc = new User32.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<User32.WNDCLASSEX>(),
            style = 0x0003, // CS_HREDRAW | CS_VREDRAW
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = _hInstance,
            hCursor = User32.LoadCursorW(0, User32.IDC_ARROW),
            hbrBackground = 0,
            lpszClassName = _className
        };

        if (User32.RegisterClassExW(ref wc) == 0)
        {
            var error = Marshal.GetLastWin32Error();
            throw new Exception($"RegisterClassEx failed with error {error}");
        }

        // Center on screen
        int screenW = User32.GetSystemMetrics(User32.SM_CXSCREEN);
        int screenH = User32.GetSystemMetrics(User32.SM_CYSCREEN);
        int x = (screenW - Theme.WindowWidth) / 2;
        int y = (screenH - Theme.WindowHeight) / 2;

        // Create borderless window
        var title = Marshal.StringToHGlobalUni("SysPilot Setup\0");
        _hWnd = User32.CreateWindowExW(
            User32.WS_EX_APPWINDOW,
            _className,
            title,
            User32.WS_POPUP | User32.WS_VISIBLE | User32.WS_SYSMENU | User32.WS_MINIMIZEBOX,
            x, y, Theme.WindowWidth, Theme.WindowHeight,
            0, 0, _hInstance, 0);
        Marshal.FreeHGlobal(title);

        if (_hWnd == 0)
        {
            var error = Marshal.GetLastWin32Error();
            throw new Exception($"CreateWindowEx failed with error {error}");
        }

        // Enable dark mode and rounded corners (Windows 11)
        int darkMode = 1;
        Dwmapi.DwmSetWindowAttribute(_hWnd, Dwmapi.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        int cornerPref = Dwmapi.DWMWCP_ROUND;
        Dwmapi.DwmSetWindowAttribute(_hWnd, Dwmapi.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

        User32.ShowWindow(_hWnd, User32.SW_SHOW);
        User32.UpdateWindow(_hWnd);
    }

    public void Run()
    {
        while (User32.GetMessageW(out var msg, 0, 0, 0))
        {
            User32.TranslateMessage(ref msg);
            User32.DispatchMessageW(ref msg);
        }
    }

    private nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case User32.WM_PAINT:
                OnPaint(hWnd);
                return 0;

            case User32.WM_ERASEBKGND:
                return 1; // Prevent flicker

            case User32.WM_LBUTTONDOWN:
                OnMouseDown(hWnd, LOWORD(lParam), HIWORD(lParam));
                return 0;

            case User32.WM_LBUTTONUP:
                OnMouseUp(hWnd, LOWORD(lParam), HIWORD(lParam));
                return 0;

            case User32.WM_MOUSEMOVE:
                OnMouseMove(hWnd, LOWORD(lParam), HIWORD(lParam));
                return 0;

            case User32.WM_SETCURSOR:
                if (LOWORD(lParam) == User32.HTCLIENT)
                {
                    User32.GetCursorPos(out var pt);
                    User32.ScreenToClient(hWnd, ref pt);
                    var cursor = (_closeHover || _installHover || _browseHover ||
                                  _checkStartMenuAllHover || _checkStartMenuUserHover || _checkDesktopHover)
                        ? User32.LoadCursorW(0, User32.IDC_HAND)
                        : User32.LoadCursorW(0, User32.IDC_ARROW);
                    User32.SetCursor(cursor);
                    return 1;
                }
                break;

            case User32.WM_TIMER:
                if (wParam == TIMER_ID && _state == InstallState.Installing)
                {
                    User32.InvalidateRect(hWnd, 0, false);
                }
                return 0;

            case User32.WM_DESTROY:
                User32.PostQuitMessage(0);
                return 0;
        }

        return User32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void OnPaint(nint hWnd)
    {
        User32.BeginPaint(hWnd, out var ps);
        User32.GetClientRect(hWnd, out var clientRect);

        int w = clientRect.Width;
        int h = clientRect.Height;

        // Double buffering: Create off-screen buffer
        var memDC = Gdi32.CreateCompatibleDC(ps.hdc);
        var memBitmap = Gdi32.CreateCompatibleBitmap(ps.hdc, w, h);
        var oldBitmap = Gdi32.SelectObject(memDC, memBitmap);

        // Create graphics from memory DC
        GdiPlus.GdipCreateFromHDC(memDC, out var graphics);
        GdiPlus.GdipSetSmoothingMode(graphics, GdiPlus.SmoothingModeAntiAlias);
        GdiPlus.GdipSetTextRenderingHint(graphics, GdiPlus.TextRenderingHintClearTypeGridFit);

        // Clear background with dark color
        GdiPlus.GdipGraphicsClear(graphics, Theme.Background);

        int centerX = w / 2;

        // Draw close button (top right)
        _closeButtonRect = new Rect(w - 40, 8, 32, 32);
        DrawCloseButton(graphics, _closeButtonRect, _closeHover);

        // Logo position
        int contentY = 50;

        // Draw logo if loaded
        if (_logo != 0)
        {
            int logoSize = 64;
            int logoX = centerX - logoSize / 2;
            GdiPlus.GdipDrawImageRectI(graphics, _logo, logoX, contentY, logoSize, logoSize);
            contentY += logoSize + 16;
        }

        // Draw title
        DrawText(graphics, "SysPilot", _titleFont, Theme.Text, 0, contentY, w, 36);
        contentY += 44;

        // Draw subtitle based on state
        string subtitle = _state switch
        {
            InstallState.Ready => "System utility for Windows",
            InstallState.Installing => _statusText,
            InstallState.Complete => _isUninstallMode ? "Uninstallation complete!" : "Installation complete!",
            InstallState.Error => _errorMessage ?? "An error occurred",
            InstallState.ConfirmUninstall => "SysPilot is already installed",
            InstallState.Uninstalling => _statusText,
            _ => ""
        };
        uint subtitleColor = _state == InstallState.Error ? Theme.Error :
                            _state == InstallState.Complete ? Theme.Success :
                            _state == InstallState.ConfirmUninstall ? Theme.TextSecondary : Theme.TextSecondary;
        DrawText(graphics, subtitle, _textFont, subtitleColor, 0, contentY, w, 24);
        contentY += 44;

        if (_state == InstallState.Ready || _state == InstallState.Installing)
        {
            // Draw progress bar
            int progressX = Theme.Padding;
            int progressW = w - Theme.Padding * 2;
            int progressH = 8;

            DrawRoundedRect(graphics, progressX, contentY, progressW, progressH, 4, Theme.ProgressBg);

            if (_progress > 0)
            {
                int fillW = Math.Max(8, (int)(progressW * _progress));
                DrawRoundedRect(graphics, progressX, contentY, fillW, progressH, 4, Theme.ProgressFill);
            }

            // Progress percentage
            if (_state == InstallState.Installing)
            {
                string percent = $"{(int)(_progress * 100)}%";
                DrawText(graphics, percent, _smallFont, Theme.TextMuted, 0, contentY + progressH + 8, w, 20);
            }

            contentY += progressH + 40;

            // Path section
            DrawTextLeft(graphics, "Install location:", _smallFont, Theme.TextMuted, Theme.Padding, contentY, 200, 20);
            contentY += 24;

            // Path box
            int pathWidth = w - Theme.Padding * 2 - 80;
            DrawRoundedRect(graphics, Theme.Padding, contentY, pathWidth, 36, 6, Theme.Surface);
            DrawTextLeft(graphics, TruncatePath(_installPath, 35), _smallFont, Theme.Text, Theme.Padding + 12, contentY, pathWidth - 12, 36);

            // Browse button
            _browseButtonRect = new Rect(w - Theme.Padding - 70, contentY, 70, 36);
            DrawButton(graphics, _browseButtonRect, "Browse", _browseHover, false);

            contentY += 48;

            // Shortcut options
            DrawTextLeft(graphics, "Create shortcuts:", _smallFont, Theme.TextMuted, Theme.Padding, contentY, 200, 20);
            contentY += 22;

            // Checkboxes - Row 1
            int checkX = Theme.Padding;
            int checkSize = 18;

            // Hit area includes label for click
            _checkStartMenuAllRect = new Rect(checkX, contentY, checkSize + 6 + 150, checkSize);
            DrawCheckbox(graphics, new Rect(checkX, contentY, checkSize, checkSize), _createStartMenuAll, _checkStartMenuAllHover);
            DrawTextLeft(graphics, "Start Menu (All Users)", _smallFont, Theme.Text, checkX + checkSize + 6, contentY, 160, checkSize);

            _checkDesktopRect = new Rect(checkX + 190, contentY, checkSize + 6 + 60, checkSize);
            DrawCheckbox(graphics, new Rect(checkX + 190, contentY, checkSize, checkSize), _createDesktopShortcut, _checkDesktopHover);
            DrawTextLeft(graphics, "Desktop", _smallFont, Theme.Text, checkX + 190 + checkSize + 6, contentY, 80, checkSize);

            contentY += 24;

            // Checkboxes - Row 2
            _checkStartMenuUserRect = new Rect(checkX, contentY, checkSize + 6 + 170, checkSize);
            DrawCheckbox(graphics, new Rect(checkX, contentY, checkSize, checkSize), _createStartMenuUser, _checkStartMenuUserHover);
            DrawTextLeft(graphics, "Start Menu (Current User)", _smallFont, Theme.Text, checkX + checkSize + 6, contentY, 180, checkSize);

            contentY += 28;
        }
        else if (_state == InstallState.ConfirmUninstall || _state == InstallState.Uninstalling)
        {
            // Draw progress bar for uninstalling
            int progressX = Theme.Padding;
            int progressW = w - Theme.Padding * 2;
            int progressH = 8;

            DrawRoundedRect(graphics, progressX, contentY, progressW, progressH, 4, Theme.ProgressBg);

            if (_progress > 0)
            {
                int fillW = Math.Max(8, (int)(progressW * _progress));
                DrawRoundedRect(graphics, progressX, contentY, fillW, progressH, 4, Theme.ProgressFill);
            }

            if (_state == InstallState.Uninstalling)
            {
                string percent = $"{(int)(_progress * 100)}%";
                DrawText(graphics, percent, _smallFont, Theme.TextMuted, 0, contentY + progressH + 8, w, 20);
            }

            contentY += progressH + 40;

            // Show install location (read-only for uninstall)
            DrawTextLeft(graphics, "Installed at:", _smallFont, Theme.TextMuted, Theme.Padding, contentY, 200, 20);
            contentY += 24;

            int pathWidth = w - Theme.Padding * 2;
            DrawRoundedRect(graphics, Theme.Padding, contentY, pathWidth, 36, 6, Theme.Surface);
            DrawTextLeft(graphics, TruncatePath(_installPath, 45), _smallFont, Theme.Text, Theme.Padding + 12, contentY, pathWidth - 12, 36);

            // No browse button in uninstall mode
            _browseButtonRect = new Rect(0, 0, 0, 0);

            contentY += 56;
        }
        else
        {
            _browseButtonRect = new Rect(0, 0, 0, 0);
            contentY += 30;
        }

        // Main action button
        int btnW = 160;
        int btnH = 44;
        _installButtonRect = new Rect(centerX - btnW / 2, contentY, btnW, btnH);

        string btnText = _state switch
        {
            InstallState.Ready => "Install",
            InstallState.Installing => "Installing...",
            InstallState.Complete => _isUninstallMode ? "Close" : "Launch",
            InstallState.Error => "Close",
            InstallState.ConfirmUninstall => "Uninstall",
            InstallState.Uninstalling => "Uninstalling...",
            _ => "Install"
        };
        bool btnEnabled = _state != InstallState.Installing && _state != InstallState.Uninstalling;
        DrawButton(graphics, _installButtonRect, btnText, _installHover && btnEnabled, true);

        GdiPlus.GdipDeleteGraphics(graphics);

        // Copy buffer to screen
        Gdi32.BitBlt(ps.hdc, 0, 0, w, h, memDC, 0, 0, Gdi32.SRCCOPY);

        // Cleanup
        Gdi32.SelectObject(memDC, oldBitmap);
        Gdi32.DeleteObject(memBitmap);
        Gdi32.DeleteDC(memDC);

        User32.EndPaint(hWnd, ref ps);
    }

    private void DrawRoundedRect(nint graphics, int x, int y, int w, int h, int radius, uint color)
    {
        GdiPlus.GdipCreatePath(0, out var path);

        float r = radius;
        float fx = x, fy = y, fw = w, fh = h;

        // Top-left arc
        GdiPlus.GdipAddPathArc(path, fx, fy, r * 2, r * 2, 180, 90);
        // Top-right arc
        GdiPlus.GdipAddPathArc(path, fx + fw - r * 2, fy, r * 2, r * 2, 270, 90);
        // Bottom-right arc
        GdiPlus.GdipAddPathArc(path, fx + fw - r * 2, fy + fh - r * 2, r * 2, r * 2, 0, 90);
        // Bottom-left arc
        GdiPlus.GdipAddPathArc(path, fx, fy + fh - r * 2, r * 2, r * 2, 90, 90);
        GdiPlus.GdipClosePathFigure(path);

        GdiPlus.GdipCreateSolidFill(color, out var brush);
        GdiPlus.GdipFillPath(graphics, brush, path);

        GdiPlus.GdipDeleteBrush(brush);
        GdiPlus.GdipDeletePath(path);
    }

    private void DrawButton(nint graphics, Rect rect, string text, bool hover, bool primary)
    {
        uint bgColor = primary
            ? (hover ? Theme.AccentHover : Theme.Accent)
            : (hover ? Theme.SurfaceHover : Theme.Surface);

        DrawRoundedRect(graphics, rect.X, rect.Y, rect.Width, rect.Height, Theme.ButtonRadius, bgColor);
        DrawText(graphics, text, _textFont, Theme.Text, rect.X, rect.Y, rect.Width, rect.Height);
    }

    private void DrawCloseButton(nint graphics, Rect rect, bool hover)
    {
        if (hover)
        {
            DrawRoundedRect(graphics, rect.X, rect.Y, rect.Width, rect.Height, 6, Theme.Error);
        }

        // Draw X as two separate lines
        GdiPlus.GdipCreatePen1(Theme.Text, 2, GdiPlus.UnitPixel, out var pen);

        float cx = rect.X + rect.Width / 2f;
        float cy = rect.Y + rect.Height / 2f;
        float size = 5f;

        // Line 1: top-left to bottom-right (\)
        GdiPlus.GdipDrawLine(graphics, pen, cx - size, cy - size, cx + size, cy + size);
        // Line 2: top-right to bottom-left (/)
        GdiPlus.GdipDrawLine(graphics, pen, cx + size, cy - size, cx - size, cy + size);

        GdiPlus.GdipDeletePen(pen);
    }

    private void DrawCheckbox(nint graphics, Rect rect, bool isChecked, bool hover)
    {
        // Background
        uint bgColor = hover ? Theme.SurfaceHover : Theme.Surface;
        DrawRoundedRect(graphics, rect.X, rect.Y, rect.Width, rect.Height, 4, bgColor);

        // Border
        GdiPlus.GdipCreatePen1(hover ? Theme.Accent : Theme.Border, 1.5f, GdiPlus.UnitPixel, out var borderPen);
        GdiPlus.GdipCreatePath(0, out var borderPath);
        float r = 4f;
        GdiPlus.GdipAddPathArc(borderPath, rect.X, rect.Y, r * 2, r * 2, 180, 90);
        GdiPlus.GdipAddPathArc(borderPath, rect.X + rect.Width - r * 2, rect.Y, r * 2, r * 2, 270, 90);
        GdiPlus.GdipAddPathArc(borderPath, rect.X + rect.Width - r * 2, rect.Y + rect.Height - r * 2, r * 2, r * 2, 0, 90);
        GdiPlus.GdipAddPathArc(borderPath, rect.X, rect.Y + rect.Height - r * 2, r * 2, r * 2, 90, 90);
        GdiPlus.GdipClosePathFigure(borderPath);
        GdiPlus.GdipDrawPath(graphics, borderPen, borderPath);
        GdiPlus.GdipDeletePen(borderPen);
        GdiPlus.GdipDeletePath(borderPath);

        // Checkmark
        if (isChecked)
        {
            GdiPlus.GdipCreatePen1(Theme.Accent, 2.5f, GdiPlus.UnitPixel, out var checkPen);
            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;

            // Draw checkmark (two lines forming a V rotated)
            GdiPlus.GdipDrawLine(graphics, checkPen, cx - 4, cy, cx - 1, cy + 3);
            GdiPlus.GdipDrawLine(graphics, checkPen, cx - 1, cy + 3, cx + 5, cy - 4);

            GdiPlus.GdipDeletePen(checkPen);
        }
    }

    private void DrawText(nint graphics, string text, nint font, uint color, int x, int y, int w, int h)
    {
        var rect = new GdiPlus.RectF(x, y, w, h);
        GdiPlus.GdipCreateSolidFill(color, out var brush);
        GdiPlus.GdipDrawString(graphics, text, text.Length, font, ref rect, _stringFormat, brush);
        GdiPlus.GdipDeleteBrush(brush);
    }

    private void DrawTextLeft(nint graphics, string text, nint font, uint color, int x, int y, int w, int h)
    {
        var rect = new GdiPlus.RectF(x, y, w, h);
        GdiPlus.GdipCreateSolidFill(color, out var brush);
        GdiPlus.GdipDrawString(graphics, text, text.Length, font, ref rect, _stringFormatLeft, brush);
        GdiPlus.GdipDeleteBrush(brush);
    }

    private void OnMouseDown(nint hWnd, int x, int y)
    {
        // Track which button was pressed
        _closePressed = _closeButtonRect.Contains(x, y);
        _installPressed = _installButtonRect.Contains(x, y) && _state != InstallState.Installing && _state != InstallState.Uninstalling;
        _browsePressed = _browseButtonRect.Contains(x, y) && _state == InstallState.Ready;

        // Checkbox pressed (only in Ready state)
        if (_state == InstallState.Ready)
        {
            _checkStartMenuAllPressed = _checkStartMenuAllRect.Contains(x, y);
            _checkStartMenuUserPressed = _checkStartMenuUserRect.Contains(x, y);
            _checkDesktopPressed = _checkDesktopRect.Contains(x, y);
        }

        // If no interactive element was pressed, allow window dragging
        bool anyPressed = _closePressed || _installPressed || _browsePressed ||
                          _checkStartMenuAllPressed || _checkStartMenuUserPressed || _checkDesktopPressed;
        if (!anyPressed)
        {
            User32.ReleaseCapture();
            User32.SendMessageW(hWnd, User32.WM_NCLBUTTONDOWN, User32.HTCAPTION, 0);
        }
    }

    private void OnMouseUp(nint hWnd, int x, int y)
    {
        // Only trigger action if mouse is still over the same element that was pressed
        if (_closePressed && _closeButtonRect.Contains(x, y))
        {
            User32.DestroyWindow(hWnd);
        }
        else if (_installPressed && _installButtonRect.Contains(x, y))
        {
            HandleInstallButton();
        }
        else if (_browsePressed && _browseButtonRect.Contains(x, y))
        {
            BrowseFolder();
        }
        else if (_checkStartMenuAllPressed && _checkStartMenuAllRect.Contains(x, y))
        {
            _createStartMenuAll = !_createStartMenuAll;
            User32.InvalidateRect(hWnd, 0, false);
        }
        else if (_checkStartMenuUserPressed && _checkStartMenuUserRect.Contains(x, y))
        {
            _createStartMenuUser = !_createStartMenuUser;
            User32.InvalidateRect(hWnd, 0, false);
        }
        else if (_checkDesktopPressed && _checkDesktopRect.Contains(x, y))
        {
            _createDesktopShortcut = !_createDesktopShortcut;
            User32.InvalidateRect(hWnd, 0, false);
        }

        // Reset pressed state
        _closePressed = false;
        _installPressed = false;
        _browsePressed = false;
        _checkStartMenuAllPressed = false;
        _checkStartMenuUserPressed = false;
        _checkDesktopPressed = false;
    }

    private void OnMouseMove(nint hWnd, int x, int y)
    {
        bool needsRepaint = false;

        bool newCloseHover = _closeButtonRect.Contains(x, y);
        if (newCloseHover != _closeHover) { _closeHover = newCloseHover; needsRepaint = true; }

        bool newInstallHover = _installButtonRect.Contains(x, y);
        if (newInstallHover != _installHover) { _installHover = newInstallHover; needsRepaint = true; }

        bool newBrowseHover = _browseButtonRect.Contains(x, y);
        if (newBrowseHover != _browseHover) { _browseHover = newBrowseHover; needsRepaint = true; }

        // Checkbox hover (only in Ready state)
        if (_state == InstallState.Ready)
        {
            bool newStartMenuAllHover = _checkStartMenuAllRect.Contains(x, y);
            if (newStartMenuAllHover != _checkStartMenuAllHover) { _checkStartMenuAllHover = newStartMenuAllHover; needsRepaint = true; }

            bool newStartMenuUserHover = _checkStartMenuUserRect.Contains(x, y);
            if (newStartMenuUserHover != _checkStartMenuUserHover) { _checkStartMenuUserHover = newStartMenuUserHover; needsRepaint = true; }

            bool newDesktopHover = _checkDesktopRect.Contains(x, y);
            if (newDesktopHover != _checkDesktopHover) { _checkDesktopHover = newDesktopHover; needsRepaint = true; }
        }

        if (needsRepaint)
        {
            User32.InvalidateRect(hWnd, 0, false);
        }
    }

    private void HandleInstallButton()
    {
        switch (_state)
        {
            case InstallState.Ready:
                StartInstallation();
                break;

            case InstallState.Complete:
                if (!_isUninstallMode)
                {
                    LaunchApp();
                }
                User32.DestroyWindow(_hWnd);
                break;

            case InstallState.Error:
                User32.DestroyWindow(_hWnd);
                break;

            case InstallState.ConfirmUninstall:
                StartUninstallation();
                break;
        }
    }

    private void StartInstallation()
    {
        // Check if SysPilot is already running
        var runningProcesses = Process.GetProcessesByName("SysPilot");
        if (runningProcesses.Length > 0)
        {
            // Ask user to close it
            int result = MessageBox(
                "SysPilot is currently running.\n\nClose it to continue installation?",
                "SysPilot Setup",
                0x04 | 0x20); // MB_YESNO | MB_ICONQUESTION

            if (result == 6) // IDYES
            {
                _statusText = "Closing SysPilot...";
                User32.InvalidateRect(_hWnd, 0, false);

                foreach (var proc in runningProcesses)
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(3000);
                    }
                    catch { }
                }
            }
            else
            {
                return; // User cancelled
            }
        }

        _state = InstallState.Installing;
        _progress = 0;
        _statusText = "Preparing...";
        User32.InvalidateRect(_hWnd, 0, false);

        // Start timer for UI updates
        User32.SetTimer(_hWnd, TIMER_ID, 50, 0);

        // Capture shortcut options
        var shortcuts = new ShortcutOptions(_createStartMenuAll, _createStartMenuUser, _createDesktopShortcut);

        // Run installation on background thread
        var hWnd = _hWnd;
        var thread = new Thread(() =>
        {
            try
            {
                PayloadInstaller.Install(_installPath, shortcuts, (progress, status) =>
                {
                    _progress = progress;
                    _statusText = status;
                });

                _state = InstallState.Complete;
            }
            catch (Exception ex)
            {
                _state = InstallState.Error;
                _errorMessage = ex.Message;
            }
            finally
            {
                User32.KillTimer(hWnd, TIMER_ID);
                User32.InvalidateRect(hWnd, 0, false);
            }
        });
        thread.Start();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(nint hWnd, string text, string caption, uint type);

    private int MessageBox(string text, string caption, uint type)
    {
        return MessageBox(_hWnd, text, caption, type);
    }

    private void StartUninstallation()
    {
        _state = InstallState.Uninstalling;
        _progress = 0;
        _statusText = "Preparing...";
        User32.InvalidateRect(_hWnd, 0, false);

        User32.SetTimer(_hWnd, TIMER_ID, 50, 0);

        var hWnd = _hWnd;
        var installDir = _installPath;
        var thread = new Thread(() =>
        {
            try
            {
                _progress = 0.1f;
                _statusText = "Stopping SysPilot...";

                // Kill running processes
                try
                {
                    foreach (var proc in Process.GetProcessesByName("SysPilot"))
                    {
                        proc.Kill();
                        proc.WaitForExit(3000);
                    }
                }
                catch { }

                _progress = 0.3f;
                _statusText = "Removing shortcuts...";

                Uninstaller.RemoveShortcuts();

                _progress = 0.5f;
                _statusText = "Removing registry entries...";

                RegistryHelper.RemoveUninstaller();

                _progress = 0.7f;
                _statusText = "Scheduling file removal...";

                // Schedule deletion via cmd (can't delete while running)
                var batchPath = Path.Combine(Path.GetTempPath(), "syspilot_uninstall.cmd");
                var batch = $"""
                    @echo off
                    timeout /t 2 /nobreak >nul
                    rmdir /s /q "{installDir}"
                    del /f "%~f0"
                    """;
                File.WriteAllText(batchPath, batch);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batchPath}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });

                _progress = 1.0f;
                _state = InstallState.Complete;
                _statusText = "Uninstallation complete!";
            }
            catch (Exception ex)
            {
                _state = InstallState.Error;
                _errorMessage = ex.Message;
            }
            finally
            {
                User32.KillTimer(hWnd, TIMER_ID);
                User32.InvalidateRect(hWnd, 0, false);
            }
        });
        thread.Start();
    }

    private void LaunchApp()
    {
        var exePath = Path.Combine(_installPath, "SysPilot.exe");
        if (File.Exists(exePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });
        }
    }

    private void BrowseFolder()
    {
        try
        {
            var clsid = Shell32.CLSID_FileOpenDialog;
            var iid = Shell32.IID_IFileOpenDialog;

            int hr = Shell32.CoCreateInstance(ref clsid, 0, Shell32.CLSCTX_INPROC_SERVER, ref iid, out var pDialog);
            if (hr != 0)
            {
                return;
            }

            try
            {
                var dialog = (IFileOpenDialog)Marshal.GetObjectForIUnknown(pDialog);

                // Set options for folder picker
                dialog.GetOptions(out uint options);
                options |= FileDialogOptions.FOS_PICKFOLDERS | FileDialogOptions.FOS_FORCEFILESYSTEM | FileDialogOptions.FOS_PATHMUSTEXIST;
                dialog.SetOptions(options);
                dialog.SetTitle("Select Installation Folder");

                // Show dialog
                hr = dialog.Show(_hWnd);
                if (hr == 0) // User clicked OK
                {
                    dialog.GetResult(out var item);
                    item.GetDisplayName(0x80058000, out var path); // SIGDN_FILESYSPATH
                    if (!string.IsNullOrEmpty(path))
                    {
                        // Append SysPilot to the selected folder
                        _installPath = Path.Combine(path, "SysPilot");
                        User32.InvalidateRect(_hWnd, 0, false);
                    }
                }
            }
            finally
            {
                Marshal.Release(pDialog);
            }
        }
        catch
        {
            // Folder dialog failed, silently ignore
        }
    }

    private static string TruncatePath(string path, int maxLen)
    {
        if (path.Length <= maxLen)
        {
            return path;
        }
        return "..." + path[(path.Length - maxLen + 3)..];
    }

    private static int LOWORD(nint value) => (int)(value.ToInt64() & 0xFFFF);
    private static int HIWORD(nint value) => (int)((value.ToInt64() >> 16) & 0xFFFF);

    public void Dispose()
    {
        if (_logo != 0)
        {
            GdiPlus.GdipDisposeImage(_logo);
        }
        if (_titleFont != 0)
        {
            GdiPlus.GdipDeleteFont(_titleFont);
        }
        if (_textFont != 0)
        {
            GdiPlus.GdipDeleteFont(_textFont);
        }
        if (_smallFont != 0)
        {
            GdiPlus.GdipDeleteFont(_smallFont);
        }
        if (_fontFamily != 0)
        {
            GdiPlus.GdipDeleteFontFamily(_fontFamily);
        }
        if (_stringFormat != 0)
        {
            GdiPlus.GdipDeleteStringFormat(_stringFormat);
        }
        if (_stringFormatLeft != 0)
        {
            GdiPlus.GdipDeleteStringFormat(_stringFormatLeft);
        }
        if (_gdipToken != 0)
        {
            GdiPlus.GdiplusShutdown(_gdipToken);
        }
        if (_className != 0)
        {
            Marshal.FreeHGlobal(_className);
        }
        CleanupTempLogo();
    }
}

// Managed Stream wrapper as COM IStream for GDI+
[ComVisible(true)]
[Guid("0000000c-0000-0000-C000-000000000046")]
internal sealed class ManagedIStream : IStream
{
    private readonly Stream _stream;

    public ManagedIStream(Stream stream) => _stream = stream;

    public void Read(byte[] pv, int cb, nint pcbRead)
    {
        int read = _stream.Read(pv, 0, cb);
        if (pcbRead != 0)
        {
            Marshal.WriteInt32(pcbRead, read);
        }
    }

    public void Write(byte[] pv, int cb, nint pcbWritten)
    {
        _stream.Write(pv, 0, cb);
        if (pcbWritten != 0)
        {
            Marshal.WriteInt32(pcbWritten, cb);
        }
    }

    public void Seek(long dlibMove, int dwOrigin, nint plibNewPosition)
    {
        long pos = _stream.Seek(dlibMove, (SeekOrigin)dwOrigin);
        if (plibNewPosition != 0)
        {
            Marshal.WriteInt64(plibNewPosition, pos);
        }
    }

    public void SetSize(long libNewSize) => _stream.SetLength(libNewSize);

    public void CopyTo(IStream pstm, long cb, nint pcbRead, nint pcbWritten)
    {
        var buffer = new byte[4096];
        long totalRead = 0;
        while (totalRead < cb)
        {
            int toRead = (int)Math.Min(buffer.Length, cb - totalRead);
            int read = _stream.Read(buffer, 0, toRead);
            if (read == 0)
            {
                break;
            }
            pstm.Write(buffer, read, 0);
            totalRead += read;
        }
        if (pcbRead != 0)
        {
            Marshal.WriteInt64(pcbRead, totalRead);
        }
        if (pcbWritten != 0)
        {
            Marshal.WriteInt64(pcbWritten, totalRead);
        }
    }

    public void Commit(int grfCommitFlags) => _stream.Flush();
    public void Revert() { }
    public void LockRegion(long libOffset, long cb, int dwLockType) { }
    public void UnlockRegion(long libOffset, long cb, int dwLockType) { }

    public void Stat(out STATSTG pstatstg, int grfStatFlag)
    {
        pstatstg = new STATSTG
        {
            cbSize = _stream.Length,
            type = 2 // STGTY_STREAM
        };
    }

    public void Clone(out IStream ppstm) =>
        throw new NotImplementedException();
}
