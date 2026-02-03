namespace SysPilot.Installer.UI;

internal static class Theme
{
    // Colors in ARGB format for GDI+ (matching SysPilot app)
    public const uint Background = 0xFF1A1A1A;      // #1A1A1A - Main background
    public const uint Surface = 0xFF252525;         // #252525 - Cards/panels
    public const uint SurfaceHover = 0xFF3A3A3A;    // #3A3A3A - Hover state
    public const uint Border = 0xFF404040;          // #404040 - Borders
    public const uint Accent = 0xFF007ACC;          // #007ACC - Blue accent
    public const uint AccentHover = 0xFF1E90FF;     // Lighter blue for hover
    public const uint Text = 0xFFE0E0E0;            // #E0E0E0 - Primary text
    public const uint TextSecondary = 0xFFA0A0A0;   // Secondary text
    public const uint TextMuted = 0xFF808080;       // #808080 - Muted text
    public const uint Success = 0xFF4CAF50;         // Green for success
    public const uint Error = 0xFFE81123;           // #E81123 - Red for errors
    public const uint ProgressBg = 0xFF2A2A2A;      // Progress bar background
    public const uint ProgressFill = 0xFF007ACC;    // Progress bar fill (accent blue)

    // Window dimensions
    public const int WindowWidth = 480;
    public const int WindowHeight = 520;
    public const int CornerRadius = 12;
    public const int ButtonRadius = 8;
    public const int ProgressRadius = 6;

    // Spacing
    public const int Padding = 24;
    public const int SmallPadding = 12;

    // Font sizes
    public const float TitleSize = 24f;
    public const float SubtitleSize = 14f;
    public const float ButtonSize = 14f;
    public const float SmallSize = 12f;
}
