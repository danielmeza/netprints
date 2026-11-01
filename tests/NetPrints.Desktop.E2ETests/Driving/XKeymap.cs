using System.Runtime.InteropServices;

namespace NetPrints.Desktop.E2ETests.Driving;

/// <summary>Reads key codes from the keymap of an X display.</summary>
public static partial class XKeymap
{
    [LibraryImport("libX11.so.6", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr XOpenDisplay(string name);

    [LibraryImport("libX11.so.6")]
    private static partial int XCloseDisplay(IntPtr display);

    [LibraryImport("libX11.so.6", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nuint XStringToKeysym(string name);

    [LibraryImport("libX11.so.6")]
    private static partial byte XKeysymToKeycode(IntPtr display, nuint keysym);

    /// <summary>The key code that produces the named key symbol (e.g. "F7") on the display.</summary>
    /// <exception cref="InvalidOperationException">The display cannot be opened, or its keymap has no such key.</exception>
    public static int KeycodeOf(string displayName, string keyName)
    {
        IntPtr display = XOpenDisplay(displayName);
        if (display == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Cannot open X display {displayName}.");
        }

        try
        {
            byte keycode = XKeysymToKeycode(display, XStringToKeysym(keyName));
            return keycode != 0 ? keycode : throw new InvalidOperationException($"The keymap of {displayName} has no key {keyName}.");
        }
        finally
        {
            _ = XCloseDisplay(display);
        }
    }
}
