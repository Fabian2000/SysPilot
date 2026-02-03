using System.Runtime.InteropServices;

namespace SysPilot.Installer.Native;

internal static partial class Shell32
{
    public static readonly Guid CLSID_ShellLink = new("00021401-0000-0000-C000-000000000046");
    public static readonly Guid IID_IShellLinkW = new("000214F9-0000-0000-C000-000000000046");
    public static readonly Guid IID_IPersistFile = new("0000010b-0000-0000-C000-000000000046");

    // File Dialog
    public static readonly Guid CLSID_FileOpenDialog = new("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7");
    public static readonly Guid IID_IFileOpenDialog = new("d57c7288-d4ad-4768-be02-9d969532d960");

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, nint hToken, out nint ppszPath);

    public static readonly Guid FOLDERID_Desktop = new("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");
    public static readonly Guid FOLDERID_Programs = new("A77F5D77-2E2B-44C3-A6A2-ABA601054A51");
    public static readonly Guid FOLDERID_CommonPrograms = new("0139D44E-6AFE-49F2-8690-3DAFCAE6FFB8");

    [LibraryImport("ole32.dll")]
    public static partial int CoCreateInstance(ref Guid rclsid, nint pUnkOuter, uint dwClsContext, ref Guid riid, out nint ppv);

    public const uint CLSCTX_INPROC_SERVER = 1;
}

// COM Interfaces for Shell Links
[ComImport]
[Guid("000214F9-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellLinkW
{
    void GetPath([MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 1)] out string pszFile, int cch, nint pfd, uint fFlags);
    void GetIDList(out nint ppidl);
    void SetIDList(nint pidl);
    void GetDescription([MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 1)] out string pszName, int cch);
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 1)] out string pszDir, int cch);
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
    void GetArguments([MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 1)] out string pszArgs, int cch);
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
    void GetHotkey(out ushort pwHotkey);
    void SetHotkey(ushort wHotkey);
    void GetShowCmd(out int piShowCmd);
    void SetShowCmd(int iShowCmd);
    void GetIconLocation([MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 1)] out string pszIconPath, int cch, out int piIcon);
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
    void Resolve(nint hwnd, uint fFlags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}

[ComImport]
[Guid("0000010b-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPersistFile
{
    void GetClassID(out Guid pClassID);
    void IsDirty();
    void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
    void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
    void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
    void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
}

// File Open Dialog for folder selection
[ComImport]
[Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOpenDialog
{
    // IModalWindow
    [PreserveSig] int Show(nint hwndOwner);

    // IFileDialog
    void SetFileTypes(uint cFileTypes, nint rgFilterSpec);
    void SetFileTypeIndex(uint iFileType);
    void GetFileTypeIndex(out uint piFileType);
    void Advise(nint pfde, out uint pdwCookie);
    void Unadvise(uint dwCookie);
    void SetOptions(uint fos);
    void GetOptions(out uint pfos);
    void SetDefaultFolder(IShellItem psi);
    void SetFolder(IShellItem psi);
    void GetFolder(out IShellItem ppsi);
    void GetCurrentSelection(out IShellItem ppsi);
    void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
    void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
    void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
    void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
    void GetResult(out IShellItem ppsi);
    void AddPlace(IShellItem psi, int fdap);
    void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
    void Close(int hr);
    void SetClientGuid(ref Guid guid);
    void ClearClientData();
    void SetFilter(nint pFilter);

    // IFileOpenDialog
    void GetResults(out nint ppenum);
    void GetSelectedItems(out nint ppsai);
}

[ComImport]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler(nint pbc, ref Guid bhid, ref Guid riid, out nint ppv);
    void GetParent(out IShellItem ppsi);
    void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
    void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
    void Compare(IShellItem psi, uint hint, out int piOrder);
}

internal static class FileDialogOptions
{
    public const uint FOS_PICKFOLDERS = 0x00000020;
    public const uint FOS_FORCEFILESYSTEM = 0x00000040;
    public const uint FOS_NOVALIDATE = 0x00000100;
    public const uint FOS_PATHMUSTEXIST = 0x00000800;
}
