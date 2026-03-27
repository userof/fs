using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;

namespace FileManager.Services;

public static class FileIconService
{
    private static readonly ConcurrentDictionary<string, Bitmap?> IconCache = new();

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbSizeFileInfo,
        uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        byte[]? lpvBits, ref BITMAPINFOHEADER lpbi, uint uUsage);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hObject, int cbBuffer, out BITMAP lpvObject);

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

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

    public static Bitmap? GetIcon(string path, bool isDirectory)
    {
        var cacheKey = isDirectory ? ".folder" : (Path.GetExtension(path)?.ToLowerInvariant() ?? ".file");
        return IconCache.GetOrAdd(cacheKey, _ => ExtractIcon(path, isDirectory));
    }

    private static Bitmap? ExtractIcon(string path, bool isDirectory)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            var shInfo = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES;
            uint fileAttributes = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;

            var result = SHGetFileInfo(
                path,
                fileAttributes,
                ref shInfo,
                (uint)Marshal.SizeOf<SHFILEINFO>(),
                flags);

            if (result == IntPtr.Zero || shInfo.hIcon == IntPtr.Zero)
                return null;

            try
            {
                return ConvertIconToBitmap(shInfo.hIcon);
            }
            finally
            {
                DestroyIcon(shInfo.hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    private static unsafe Bitmap? ConvertIconToBitmap(IntPtr hIcon)
    {
        if (!GetIconInfo(hIcon, out var iconInfo))
            return null;

        try
        {
            GetObject(iconInfo.hbmColor, Marshal.SizeOf<BITMAP>(), out var bmp);
            int width = bmp.bmWidth;
            int height = bmp.bmHeight;

            if (width <= 0 || height <= 0)
                return null;

            var bih = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0
            };

            var pixels = new byte[width * height * 4];
            var hdc = CreateCompatibleDC(IntPtr.Zero);
            GetDIBits(hdc, iconInfo.hbmColor, 0, (uint)height, pixels, ref bih, 0);
            DeleteDC(hdc);

            bool hasAlpha = false;
            for (int i = 3; i < pixels.Length; i += 4)
            {
                if (pixels[i] != 0) { hasAlpha = true; break; }
            }

            if (!hasAlpha)
            {
                var maskPixels = new byte[width * height * 4];
                var maskBih = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0
                };
                var hdc2 = CreateCompatibleDC(IntPtr.Zero);
                GetDIBits(hdc2, iconInfo.hbmMask, 0, (uint)height, maskPixels, ref maskBih, 0);
                DeleteDC(hdc2);

                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i + 3] = (maskPixels[i] == 0 && maskPixels[i + 1] == 0 && maskPixels[i + 2] == 0)
                        ? (byte)255 : (byte)0;
                }
            }

            var wb = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888);

            using (var fb = wb.Lock())
            {
                for (int y = 0; y < height; y++)
                {
                    var src = pixels.AsSpan(y * width * 4, width * 4);
                    var dst = new Span<byte>((byte*)fb.Address + y * fb.RowBytes, width * 4);
                    src.CopyTo(dst);
                }
            }

            return wb;
        }
        finally
        {
            if (iconInfo.hbmColor != IntPtr.Zero) DeleteObject(iconInfo.hbmColor);
            if (iconInfo.hbmMask != IntPtr.Zero) DeleteObject(iconInfo.hbmMask);
        }
    }
}
