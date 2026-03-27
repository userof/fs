using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace FileManager.Services;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class ShellContextMenuService
{
    public static void ShowContextMenu(string filePath, IntPtr hwnd, int x, int y)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            ShowShellMenu(filePath, hwnd, x, y);
        }
        catch { }
    }

    private static void ShowShellMenu(string path, IntPtr hwnd, int x, int y)
    {
        var desktop = GetDesktopFolder();
        if (desktop == null) return;

        try
        {
            var parentPath = System.IO.Path.GetDirectoryName(path);
            if (parentPath == null) return;

            // Parse parent folder
            int hr = SHParseDisplayName(parentPath, IntPtr.Zero, out var parentPidl, 0, out _);
            if (hr != 0 || parentPidl == IntPtr.Zero) return;

            try
            {
                hr = SHBindToObject(desktop, parentPidl, IntPtr.Zero,
                    ref IID_IShellFolder, out var folderPtr);
                if (hr != 0) return;

                var folder = (IShellFolder)Marshal.GetObjectForIUnknown(folderPtr);
                Marshal.Release(folderPtr);

                try
                {
                    // Parse the child item
                    var fileName = System.IO.Path.GetFileName(path);
                    hr = folder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, fileName,
                        out _, out var childPidl, ref hr);
                    if (hr != 0 || childPidl == IntPtr.Zero) return;

                    try
                    {
                        var pidlArray = new IntPtr[] { childPidl };
                        hr = folder.GetUIObjectOf(hwnd, 1, pidlArray,
                            ref IID_IContextMenu, IntPtr.Zero, out var ctxMenuPtr);
                        if (hr != 0) return;

                        var contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(ctxMenuPtr);
                        Marshal.Release(ctxMenuPtr);

                        try
                        {
                            var hMenu = CreatePopupMenu();
                            if (hMenu == IntPtr.Zero) return;

                            try
                            {
                                contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF,
                                    CMF_NORMAL | CMF_EXPLORE);

                                uint cmd = TrackPopupMenuEx(hMenu,
                                    TPM_RETURNCMD | TPM_LEFTALIGN | TPM_TOPALIGN,
                                    x, y, hwnd, IntPtr.Zero);

                                if (cmd >= 1)
                                {
                                    var info = new CMINVOKECOMMANDINFO
                                    {
                                        cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFO>(),
                                        fMask = 0,
                                        hwnd = hwnd,
                                        lpVerb = (IntPtr)(cmd - 1),
                                        lpParameters = IntPtr.Zero,
                                        lpDirectory = IntPtr.Zero,
                                        nShow = 1, // SW_SHOWNORMAL
                                        dwHotKey = 0,
                                        hIcon = IntPtr.Zero
                                    };

                                    contextMenu.InvokeCommand(ref info);
                                }
                            }
                            finally
                            {
                                DestroyMenu(hMenu);
                            }
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(contextMenu);
                        }
                    }
                    finally
                    {
                        CoTaskMemFree(childPidl);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(folder);
                }
            }
            finally
            {
                CoTaskMemFree(parentPidl);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(desktop);
        }
    }

    private static IShellFolder? GetDesktopFolder()
    {
        int hr = SHGetDesktopFolder(out var ptr);
        if (hr != 0 || ptr == IntPtr.Zero) return null;
        var folder = (IShellFolder)Marshal.GetObjectForIUnknown(ptr);
        Marshal.Release(ptr);
        return folder;
    }

    // ── COM interfaces ──

    private static Guid IID_IShellFolder = new("000214E6-0000-0000-C000-000000000046");
    private static Guid IID_IContextMenu = new("000214E4-0000-0000-C000-000000000046");

    private const uint CMF_NORMAL = 0x00000000;
    private const uint CMF_EXPLORE = 0x00000004;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_LEFTALIGN = 0x0000;
    private const uint TPM_TOPALIGN = 0x0000;

    [DllImport("shell32.dll")]
    private static extern int SHGetDesktopFolder(out IntPtr ppshf);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(
        string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

    [DllImport("shell32.dll")]
    private static extern int SHBindToObject(
        [MarshalAs(UnmanagedType.Interface)] IShellFolder psf,
        IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(
        IntPtr hMenu, uint flags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    [StructLayout(LayoutKind.Sequential)]
    private struct CMINVOKECOMMANDINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    private interface IShellFolder
    {
        int ParseDisplayName(IntPtr hwnd, IntPtr pbc,
            [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
            out uint pchEaten, out IntPtr ppidl, ref int pdwAttributes);

        int EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);

        int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

        int CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);

        int GetAttributesOf(uint cidl,
            [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
            ref uint rgfInOut);

        int GetUIObjectOf(IntPtr hwndOwner, uint cidl,
            [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
            ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);

        int GetDisplayNameOf(IntPtr pidl, uint uFlags, out IntPtr pName);

        int SetNameOf(IntPtr hwnd, IntPtr pidl,
            [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E4-0000-0000-C000-000000000046")]
    private interface IContextMenu
    {
        int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst,
            uint idCmdLast, uint uFlags);

        int InvokeCommand(ref CMINVOKECOMMANDINFO pici);

        int GetCommandString(uint idCmd, uint uType, IntPtr pReserved,
            IntPtr pszName, uint cchMax);
    }
}
