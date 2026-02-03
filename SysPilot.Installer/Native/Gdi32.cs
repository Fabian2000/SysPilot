using System.Runtime.InteropServices;

namespace SysPilot.Installer.Native;

internal static partial class Gdi32
{
    public const int SRCCOPY = 0x00CC0020;

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

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateSolidBrush(uint color);

    [LibraryImport("gdi32.dll")]
    public static partial int SetBkMode(nint hdc, int mode);

    [LibraryImport("gdi32.dll")]
    public static partial uint SetTextColor(nint hdc, uint color);

    public const int TRANSPARENT = 1;
}

// GDI+ Flat API
internal static partial class GdiPlus
{
    public const int Ok = 0;
    public const int UnitPixel = 2;
    public const int SmoothingModeAntiAlias = 4;
    public const int TextRenderingHintClearTypeGridFit = 5;
    public const int StringAlignmentCenter = 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct GdiplusStartupInput
    {
        public uint GdiplusVersion;
        public nint DebugEventCallback;
        public int SuppressBackgroundThread;
        public int SuppressExternalCodecs;

        public static GdiplusStartupInput Default => new() { GdiplusVersion = 1 };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GdiplusStartupOutput
    {
        public nint NotificationHook;
        public nint NotificationUnhook;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RectF
    {
        public float X, Y, Width, Height;
        public RectF(float x, float y, float w, float h) { X = x; Y = y; Width = w; Height = h; }
    }

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

    // Brushes
    [LibraryImport("gdiplus.dll")]
    public static partial int GdipCreateSolidFill(uint color, out nint brush);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDeleteBrush(nint brush);

    // Pens
    [LibraryImport("gdiplus.dll")]
    public static partial int GdipCreatePen1(uint color, float width, int unit, out nint pen);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDeletePen(nint pen);

    // Rectangles
    [LibraryImport("gdiplus.dll")]
    public static partial int GdipFillRectangle(nint graphics, nint brush, float x, float y, float width, float height);

    // Rounded Rectangles via Path
    [LibraryImport("gdiplus.dll")]
    public static partial int GdipCreatePath(int fillMode, out nint path);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDeletePath(nint path);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipAddPathArc(nint path, float x, float y, float width, float height, float startAngle, float sweepAngle);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipAddPathLine(nint path, float x1, float y1, float x2, float y2);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipClosePathFigure(nint path);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipFillPath(nint graphics, nint brush, nint path);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDrawPath(nint graphics, nint pen, nint path);

    // Fonts
    [LibraryImport("gdiplus.dll")]
    public static partial int GdipCreateFontFamilyFromName([MarshalAs(UnmanagedType.LPWStr)] string name, nint fontCollection, out nint fontFamily);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDeleteFontFamily(nint fontFamily);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipCreateFont(nint fontFamily, float emSize, int style, int unit, out nint font);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDeleteFont(nint font);

    // String Format
    [LibraryImport("gdiplus.dll")]
    public static partial int GdipCreateStringFormat(int formatAttributes, int language, out nint format);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDeleteStringFormat(nint format);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipSetStringFormatAlign(nint format, int align);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipSetStringFormatLineAlign(nint format, int align);

    // Draw String
    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDrawString(nint graphics, [MarshalAs(UnmanagedType.LPWStr)] string str, int length, nint font, ref RectF layoutRect, nint stringFormat, nint brush);

    // Draw Line (für X-Button)
    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDrawLine(nint graphics, nint pen, float x1, float y1, float x2, float y2);

    // Path figure control
    [LibraryImport("gdiplus.dll")]
    public static partial int GdipStartPathFigure(nint path);

    // Images
    [LibraryImport("gdiplus.dll")]
    public static partial int GdipCreateBitmapFromStream(nint stream, out nint bitmap);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDisposeImage(nint image);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipDrawImageRectI(nint graphics, nint image, int x, int y, int width, int height);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipGetImageWidth(nint image, out int width);

    [LibraryImport("gdiplus.dll")]
    public static partial int GdipGetImageHeight(nint image, out int height);

    // For loading from embedded resource
    [LibraryImport("gdiplus.dll")]
    public static partial int GdipLoadImageFromStream(nint stream, out nint image);

    // Load from file (simpler alternative)
    [LibraryImport("gdiplus.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int GdipLoadImageFromFile(string filename, out nint image);
}
