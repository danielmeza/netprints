using System.Runtime.InteropServices;

namespace NetPrints.Desktop.E2ETests.Driving;

/// <summary>Reads the current pointer cursor of an X display through the XFixes extension.</summary>
public static partial class XFixes
{
    [LibraryImport("libX11.so.6", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr XOpenDisplay(string name);

    [LibraryImport("libX11.so.6")]
    private static partial int XCloseDisplay(IntPtr display);

    [LibraryImport("libX11.so.6")]
    private static partial int XFree(IntPtr data);

    [LibraryImport("libXfixes.so.3")]
    private static partial IntPtr XFixesGetCursorImage(IntPtr display);

    // XFixesCursorImage offsets on 64-bit.
    private const int SerialOffset = 16;
    private const int NameOffset = 40;

    /// <summary>
    /// The cursor's name (e.g. "left_ptr", "fleur"), or, for a cursor created from an image
    /// (Avalonia loads theme images), "image:WxH:XHOT,YHOT:SERIAL".
    /// </summary>
    public static string CursorName(string displayName)
    {
        IntPtr display = XOpenDisplay(displayName);
        if (display == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Cannot open X display {displayName}.");
        }

        try
        {
            IntPtr image = XFixesGetCursorImage(display);
            if (image == IntPtr.Zero)
            {
                throw new InvalidOperationException("XFixesGetCursorImage failed.");
            }

            try
            {
                string? name = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(image, NameOffset));
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }

                int width = (ushort)Marshal.ReadInt16(image, 4), height = (ushort)Marshal.ReadInt16(image, 6);
                int xhot = (ushort)Marshal.ReadInt16(image, 8), yhot = (ushort)Marshal.ReadInt16(image, 10);
                return FormattableString.Invariant($"image:{width}x{height}:{xhot},{yhot}:{Marshal.ReadInt64(image, SerialOffset)}");
            }
            finally
            {
                _ = XFree(image);
            }
        }
        finally
        {
            _ = XCloseDisplay(display);
        }
    }
}
