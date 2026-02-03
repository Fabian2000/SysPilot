using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;
using IOSearchOption = System.IO.SearchOption;

namespace SysPilot.Helpers;

/// <summary>
/// Helper class for disk cleanup operations
/// </summary>
public static class CleanupHelper
{
    [DllImport("shell32.dll")]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    /// <summary>
    /// Represents a cleanup target with size information
    /// </summary>
    public class CleanupTarget
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public long SizeBytes { get; set; }
        public string SizeFormatted => SystemHelper.FormatBytes(SizeBytes);
        public int FileCount { get; set; }
        public bool IsSelected { get; set; } = true;
    }

    /// <summary>
    /// Progress information for cleanup operations
    /// </summary>
    public class CleanupProgress
    {
        // Fields for Interlocked operations
        private int _filesProcessed;
        private int _filesDeleted;
        private int _filesFailed;
        private long _bytesFreed;

        public int FilesProcessed { get => _filesProcessed; set => _filesProcessed = value; }
        public int TotalFiles { get; set; }
        public long TotalBytes { get; set; }
        public int FilesDeleted { get => _filesDeleted; set => _filesDeleted = value; }
        public int FilesFailed { get => _filesFailed; set => _filesFailed = value; }
        public long BytesFreed { get => _bytesFreed; set => _bytesFreed = value; }
        public string CurrentFile { get; set; } = "";
        public bool IsIndeterminate { get; set; }

        public void IncrementProcessed() => Interlocked.Increment(ref _filesProcessed);
        public void IncrementDeleted() => Interlocked.Increment(ref _filesDeleted);
        public void IncrementFailed() => Interlocked.Increment(ref _filesFailed);
        public void AddBytesFreed(long bytes) => Interlocked.Add(ref _bytesFreed, bytes);
    }

    /// <summary>
    /// Gets all available cleanup targets with their sizes
    /// </summary>
    public static async Task<List<CleanupTarget>> GetCleanupTargetsAsync()
    {
        var targets = new List<CleanupTarget>();

        await Task.Run(() =>
        {
            // User temp folder
            var userTemp = Path.GetTempPath();
            var (userTempSize, userTempCount) = GetDirectorySize(userTemp);
            targets.Add(new CleanupTarget
            {
                Name = "User Temp Files",
                Path = userTemp,
                SizeBytes = userTempSize,
                FileCount = userTempCount
            });

            // Windows temp folder
            var windowsTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
            if (Directory.Exists(windowsTemp))
            {
                var (winTempSize, winTempCount) = GetDirectorySize(windowsTemp);
                targets.Add(new CleanupTarget
                {
                    Name = "Windows Temp Files",
                    Path = windowsTemp,
                    SizeBytes = winTempSize,
                    FileCount = winTempCount
                });
            }

            // Windows Update cache
            var updateCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
            if (Directory.Exists(updateCache))
            {
                var (updateSize, updateCount) = GetDirectorySize(updateCache);
                targets.Add(new CleanupTarget
                {
                    Name = "Windows Update Cache",
                    Path = updateCache,
                    SizeBytes = updateSize,
                    FileCount = updateCount
                });
            }

            // Prefetch
            var prefetch = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
            if (Directory.Exists(prefetch))
            {
                var (prefetchSize, prefetchCount) = GetDirectorySize(prefetch);
                targets.Add(new CleanupTarget
                {
                    Name = "Prefetch Files",
                    Path = prefetch,
                    SizeBytes = prefetchSize,
                    FileCount = prefetchCount
                });
            }

            // Browser caches
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // Chrome cache
            var chromeCache = Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache");
            if (Directory.Exists(chromeCache))
            {
                var (chromeSize, chromeCount) = GetDirectorySize(chromeCache);
                targets.Add(new CleanupTarget
                {
                    Name = "Chrome Cache",
                    Path = chromeCache,
                    SizeBytes = chromeSize,
                    FileCount = chromeCount,
                    IsSelected = false
                });
            }

            // Edge cache
            var edgeCache = Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache");
            if (Directory.Exists(edgeCache))
            {
                var (edgeSize, edgeCount) = GetDirectorySize(edgeCache);
                targets.Add(new CleanupTarget
                {
                    Name = "Edge Cache",
                    Path = edgeCache,
                    SizeBytes = edgeSize,
                    FileCount = edgeCount,
                    IsSelected = false
                });
            }

            // Firefox cache
            var firefoxProfiles = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
            if (Directory.Exists(firefoxProfiles))
            {
                try
                {
                    foreach (var profile in Directory.GetDirectories(firefoxProfiles))
                    {
                        var cache = Path.Combine(profile, "cache2");
                        if (Directory.Exists(cache))
                        {
                            var (ffSize, ffCount) = GetDirectorySize(cache);
                            targets.Add(new CleanupTarget
                            {
                                Name = $"Firefox Cache ({Path.GetFileName(profile)})",
                                Path = cache,
                                SizeBytes = ffSize,
                                FileCount = ffCount,
                                IsSelected = false
                            });
                        }
                    }
                }
                catch { }
            }

            // Recycle Bin size (approximate)
            try
            {
                var recycleBin = new CleanupTarget
                {
                    Name = "Recycle Bin",
                    Path = "$RECYCLE.BIN",
                    SizeBytes = GetRecycleBinSize(),
                    FileCount = 0,
                    IsSelected = false
                };
                targets.Add(recycleBin);
            }
            catch { }
        });

        return [.. targets.Where(t => t.SizeBytes > 0).OrderByDescending(t => t.SizeBytes)];
    }

    /// <summary>
    /// Gets the size and file count of a directory
    /// </summary>
    private static (long Size, int Count) GetDirectorySize(string path)
    {
        long size = 0;
        int count = 0;

        try
        {
            var files = Directory.EnumerateFiles(path, "*", IOSearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    var info = new FileInfo(file);
                    size += info.Length;
                    count++;
                }
                catch { }
            }
        }
        catch { }

        return (size, count);
    }

    /// <summary>
    /// Gets recycle bin size using Shell API (current user only)
    /// </summary>
    private static long GetRecycleBinSize()
    {
        try
        {
            var info = new SHQUERYRBINFO
            {
                cbSize = Marshal.SizeOf<SHQUERYRBINFO>()
            };

            int result = SHQueryRecycleBin(null, ref info);
            if (result == 0) // S_OK
            {
                return info.i64Size;
            }
        }
        catch { }
        return 0;
    }

    /// <summary>
    /// Cleans up files in the specified targets
    /// </summary>
    public static async Task CleanupAsync(
        IEnumerable<CleanupTarget> targets,
        IProgress<CleanupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var progressInfo = new CleanupProgress();

        // Count total files
        foreach (var target in targets.Where(t => t.IsSelected))
        {
            progressInfo.TotalBytes += target.SizeBytes;
            if (target.Path == "$RECYCLE.BIN")
            {
                progressInfo.TotalFiles += 1; // Treat as one operation
            }
            else
            {
                progressInfo.TotalFiles += target.FileCount;
            }
        }

        progress?.Report(progressInfo);

        foreach (var target in targets.Where(t => t.IsSelected))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (target.Path == "$RECYCLE.BIN")
            {
                // Show indeterminate progress
                progressInfo.CurrentFile = "Emptying Recycle Bin...";
                progressInfo.IsIndeterminate = true;
                progress?.Report(progressInfo);

                // Empty recycle bin using Shell API
                var recycleBinSize = target.SizeBytes;
                var success = await Task.Run(() =>
                {
                    try
                    {
                        // Try without NOPROGRESSUI - let Windows show its own progress
                        int result = SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOSOUND);
                        return result == 0; // S_OK
                    }
                    catch
                    {
                        return false;
                    }
                }, cancellationToken);

                progressInfo.CurrentFile = "";
                progressInfo.IsIndeterminate = false;
                progressInfo.FilesProcessed++;
                if (success)
                {
                    progressInfo.FilesDeleted++;
                    progressInfo.BytesFreed += recycleBinSize;
                }
                progress?.Report(progressInfo);
                continue;
            }

            await Task.Run(() =>
            {
                try
                {
                    var files = Directory.EnumerateFiles(target.Path, "*", IOSearchOption.AllDirectories);
                    long lastReportTicks = DateTime.UtcNow.Ticks;
                    const long reportIntervalTicks = 100 * TimeSpan.TicksPerMillisecond; // 100ms

                    Parallel.ForEach(files, new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount,
                        CancellationToken = cancellationToken
                    }, file =>
                    {
                        try
                        {
                            var info = new FileInfo(file);
                            long fileSize = info.Length;

                            File.Delete(file);

                            progressInfo.AddBytesFreed(fileSize);
                            progressInfo.IncrementDeleted();
                        }
                        catch
                        {
                            progressInfo.IncrementFailed();
                        }
                        finally
                        {
                            progressInfo.IncrementProcessed();

                            // Throttle progress reports (thread-safe)
                            var nowTicks = DateTime.UtcNow.Ticks;
                            var last = Interlocked.Read(ref lastReportTicks);
                            if (nowTicks - last >= reportIntervalTicks)
                            {
                                if (Interlocked.CompareExchange(ref lastReportTicks, nowTicks, last) == last)
                                {
                                    progress?.Report(progressInfo);
                                }
                            }
                        }
                    });

                    // Final report
                    progress?.Report(progressInfo);

                    // Try to delete empty directories
                    try
                    {
                        foreach (var dir in Directory.EnumerateDirectories(target.Path, "*", IOSearchOption.AllDirectories)
                            .OrderByDescending(d => d.Length))
                        {
                            try
                            {
                                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                                {
                                    Directory.Delete(dir);
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
                catch { }
            }, cancellationToken);
        }

        // Final 100% report
        progressInfo.BytesFreed = progressInfo.TotalBytes;
        progressInfo.CurrentFile = "";
        progress?.Report(progressInfo);
    }

    /// <summary>
    /// Flushes the DNS cache
    /// </summary>
    public static async Task<bool> FlushDnsAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/flushdns",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(psi);
            if (process is not null)
            {
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
        }
        catch { }
        return false;
    }
}
