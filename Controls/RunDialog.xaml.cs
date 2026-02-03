using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text;
using DrawingIcon = System.Drawing.Icon;

namespace SysPilot.Controls;

public partial class RunDialog : Window
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_SMALLICON = 0x1;

    // COM interfaces for resolving shortcuts
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
    private List<ProgramEntry> _allPrograms = [];
    private bool _isUpdatingText;

    public class ProgramEntry
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public ImageSource? Icon { get; set; }
    }

    public RunDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        CommandInput.Focus();

        // Load file list in background (fast)
        var files = await Task.Run(() =>
        {
            var result = new List<(string name, string path)>();
            var paths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs)
            };

            foreach (var basePath in paths)
            {
                if (!Directory.Exists(basePath))
                {
                    continue;
                }
                try
                {
                    foreach (var file in Directory.EnumerateFiles(basePath, "*.lnk", SearchOption.AllDirectories))
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        if (!string.IsNullOrEmpty(name) && !name.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add((name, file));
                        }
                    }
                }
                catch { }
            }
            return result;
        });

        // Build program list without icons first (instant)
        var programs = new List<ProgramEntry>();
        foreach (var (name, path) in files)
        {
            programs.Add(new ProgramEntry
            {
                Name = name,
                Path = path,
                Icon = null
            });
        }

        // Add common system commands
        var systemCmds = new (string name, string cmd)[]
        {
            ("cmd - Command Prompt", "cmd"),
            ("powershell", "powershell"),
            ("regedit - Registry Editor", "regedit"),
            ("msconfig - System Configuration", "msconfig"),
            ("devmgmt.msc - Device Manager", "devmgmt.msc"),
            ("services.msc - Services", "services.msc"),
            ("control - Control Panel", "control"),
            ("taskmgr - Task Manager", "taskmgr"),
            ("diskmgmt.msc - Disk Management", "diskmgmt.msc"),
            ("compmgmt.msc - Computer Management", "compmgmt.msc"),
            ("eventvwr.msc - Event Viewer", "eventvwr.msc"),
            ("dxdiag - DirectX Diagnostics", "dxdiag"),
            ("winver - Windows Version", "winver"),
            ("calc - Calculator", "calc"),
            ("notepad", "notepad"),
            ("mspaint - Paint", "mspaint"),
            ("explorer - File Explorer", "explorer"),
        };

        foreach (var (name, cmd) in systemCmds)
        {
            programs.Add(new ProgramEntry
            {
                Name = name,
                Path = cmd,
                Icon = null
            });
        }

        _allPrograms = [.. programs.DistinctBy(p => p.Name).OrderBy(p => p.Name)];

        // Load icons in background batches
        _ = LoadIconsAsync();
    }

    private async Task LoadIconsAsync()
    {
        const int batchSize = 10;
        for (int i = 0; i < _allPrograms.Count; i += batchSize)
        {
            var batch = _allPrograms.Skip(i).Take(batchSize).ToList();
            foreach (var entry in batch)
            {
                if (entry.Icon is null && entry.Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    entry.Icon = GetFileIcon(entry.Path);
                }
            }
            await Task.Delay(1); // Yield to UI
        }
    }

    private static string? ResolveShortcut(string lnkPath)
    {
        try
        {
            var link = (IShellLink)new ShellLink();
            ((IPersistFile)link).Load(lnkPath, 0);

            var sb = new StringBuilder(260);
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            var target = sb.ToString();
            return string.IsNullOrEmpty(target) ? null : target;
        }
        catch
        {
            return null;
        }
    }

    private ImageSource? GetFileIcon(string path)
    {
        try
        {
            // Use SHGetFileInfo - works for shortcuts, exes, and all file types
            var shfi = new SHFILEINFO();
            var result = SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_SMALLICON);

            if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                return null!;

            try
            {
                var icon = DrawingIcon.FromHandle(shfi.hIcon);
                var bitmap = icon.ToBitmap();
                var hBitmap = bitmap.GetHbitmap();

                try
                {
                    var source = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
            finally
            {
                DestroyIcon(shfi.hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    public static void Show(Window? owner = null)
    {
        var dialog = new RunDialog
        {
            Owner = owner ?? Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    private void CommandInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingText)
        {
            return;
        }

        var text = CommandInput.Text.Trim();
        if (string.IsNullOrEmpty(text) || text.Length < 1)
        {
            AutocompletePopup.IsOpen = false;
            return;
        }

        var matches = _allPrograms
            .Where(p => p.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Take(10).ToList();

        if (matches.Count > 0)
        {
            SuggestionsList.ItemsSource = matches;
            SuggestionsList.SelectedIndex = 0;
            AutocompletePopup.IsOpen = true;
        }
        else
        {
            AutocompletePopup.IsOpen = false;
        }
    }

    private void CommandInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (AutocompletePopup.IsOpen && SuggestionsList.SelectedItem is not null)
            {
                SelectSuggestion();
            }
            else
            {
                ExecuteCommand();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (AutocompletePopup.IsOpen)
            {
                AutocompletePopup.IsOpen = false;
            }
            else
            {
                Close();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Down && AutocompletePopup.IsOpen && SuggestionsList.Items.Count > 0)
        {
            if (SuggestionsList.SelectedIndex < SuggestionsList.Items.Count - 1)
            {
                SuggestionsList.SelectedIndex++;
                SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Up && AutocompletePopup.IsOpen && SuggestionsList.Items.Count > 0)
        {
            if (SuggestionsList.SelectedIndex > 0)
            {
                SuggestionsList.SelectedIndex--;
                SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
            }
            e.Handled = true;
        }
    }

    private void SuggestionsList_Click(object sender, MouseButtonEventArgs e)
    {
        if (SuggestionsList.SelectedItem is not null)
        {
            SelectSuggestion();
        }
    }

    private void SelectSuggestion()
    {
        if (SuggestionsList.SelectedItem is ProgramEntry entry)
        {
            _isUpdatingText = true;
            CommandInput.Text = entry.Path;
            CommandInput.CaretIndex = CommandInput.Text.Length;
            _isUpdatingText = false;
            AutocompletePopup.IsOpen = false;
            CommandInput.Focus();
        }
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        ExecuteCommand();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ExecuteCommand()
    {
        var command = CommandInput.Text.Trim();
        if (string.IsNullOrEmpty(command))
        {
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                UseShellExecute = true
            };

            if (RunAsAdminCheck.IsChecked == true)
            {
                startInfo.Verb = "runas";
            }

            Process.Start(startInfo);
            Close();
        }
        catch (Exception ex)
        {
            CustomDialog.Show(
                "Error",
                $"Could not execute command:\n{ex.Message}",
                CustomDialog.DialogType.Error);
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SuggestionsList.IsFocused && SuggestionsList.SelectedItem is not null)
        {
            SelectSuggestion();
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }
}
