using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace FileManager.Services;

/// <summary>
/// Manages clipboard operations with OS interop.
/// On Windows, copies files to the real clipboard (FileDropList) so
/// pasting in Explorer works, and reads from OS clipboard on paste.
/// </summary>
public static class ClipboardService
{
    // Internal state for cut tracking (OS clipboard doesn't have a "cut" concept natively)
    private static bool _isCut;
    private static List<string> _internalPaths = new();

    public static bool IsCut => _isCut;

    public static async Task CopyFiles(IEnumerable<string> paths, bool isCut)
    {
        var pathList = paths.ToList();
        _internalPaths = pathList;
        _isCut = isCut;

        var clipboard = GetClipboard();
        if (clipboard == null) return;

        // Put file paths as text on clipboard (universally supported)
        var text = string.Join(Environment.NewLine, pathList);
        await clipboard.SetTextAsync(text);

        // On Windows, also set file drop list via OLE clipboard
        if (OperatingSystem.IsWindows())
        {
            try { SetFileDropList(pathList); }
            catch { /* fallback: text clipboard is already set */ }
        }
    }

    /// <summary>
    /// Get files from clipboard. Prefers internal state, falls back to OS clipboard text.
    /// </summary>
    public static async Task<(List<string> paths, bool isCut)> GetFiles()
    {
        // First check internal clipboard (has cut/copy state)
        if (_internalPaths.Count > 0 && _internalPaths.All(p => File.Exists(p) || Directory.Exists(p)))
            return (_internalPaths, _isCut);

        // Try OS clipboard text (might contain file paths from external apps)
        var clipboard = GetClipboard();
        if (clipboard != null)
        {
            #pragma warning disable CS0618 // GetTextAsync is obsolete
            var text = await clipboard.GetTextAsync();
            #pragma warning restore CS0618
            if (!string.IsNullOrWhiteSpace(text))
            {
                var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var validPaths = lines.Where(l => File.Exists(l) || Directory.Exists(l)).ToList();
                if (validPaths.Count > 0)
                    return (validPaths, false); // External paste is always copy
            }
        }

        return (new List<string>(), false);
    }

    public static void ClearIfCut()
    {
        if (_isCut)
        {
            _internalPaths.Clear();
            _isCut = false;
        }
    }

    public static async Task CopyText(string text)
    {
        var clipboard = GetClipboard();
        if (clipboard != null)
            await clipboard.SetTextAsync(text);
    }

    private static IClipboard? GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow?.Clipboard;
        return null;
    }

    // ── Windows OLE clipboard for file drop ──

    [DllImport("ole32.dll")]
    private static extern int OleSetClipboard(System.Runtime.InteropServices.ComTypes.IDataObject pDataObj);

    [DllImport("ole32.dll")]
    private static extern int OleFlushClipboard();

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateDataObject(
        IntPtr pidlFolder, uint cidl, IntPtr[] apidl,
        System.Runtime.InteropServices.ComTypes.IDataObject pdtInner,
        ref Guid riid, out IntPtr ppv);

    private static void SetFileDropList(List<string> paths)
    {
        // Use a simpler approach: write HDROP format to clipboard via Win32
        if (!OpenClipboard(IntPtr.Zero)) return;
        try
        {
            EmptyClipboard();

            // Build DROPFILES structure
            var fileList = string.Join('\0', paths) + "\0\0";
            var dropFilesSize = 20; // DROPFILES header size
            var byteCount = fileList.Length * 2; // Unicode
            var totalSize = dropFilesSize + byteCount;

            var hGlobal = GlobalAlloc(0x0042, (UIntPtr)totalSize); // GMEM_MOVEABLE | GMEM_ZEROINIT
            if (hGlobal == IntPtr.Zero) return;

            var ptr = GlobalLock(hGlobal);
            if (ptr == IntPtr.Zero) { GlobalFree(hGlobal); return; }

            try
            {
                // Write DROPFILES header
                Marshal.WriteInt32(ptr, 0, 20);     // pFiles offset
                Marshal.WriteInt32(ptr, 4, 0);      // pt.x
                Marshal.WriteInt32(ptr, 8, 0);      // pt.y
                Marshal.WriteInt32(ptr, 12, 0);     // fNC
                Marshal.WriteInt32(ptr, 16, 1);     // fWide = TRUE (Unicode)

                // Write file paths
                var dest = IntPtr.Add(ptr, 20);
                var bytes = System.Text.Encoding.Unicode.GetBytes(fileList);
                Marshal.Copy(bytes, 0, dest, bytes.Length);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            SetClipboardData(CF_HDROP, hGlobal);
            // Don't free hGlobal — clipboard owns it now
        }
        finally
        {
            CloseClipboard();
        }
    }

    private const uint CF_HDROP = 15;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
