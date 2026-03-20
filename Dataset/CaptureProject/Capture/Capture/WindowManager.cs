using System.Runtime.InteropServices;
using System.Text;

namespace Capture;

public static class WindowManager {
    
    private static readonly List<Window> Windows = [];
    
    public static List<Window> GetOpenWindows() {
        EnumWindows(EnumTheWindows, IntPtr.Zero);
        List<Window> clone = new(Windows);
        Windows.Clear();
        return clone;
    }

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsWindowVisible(IntPtr hWnd);

    
    private static bool EnumTheWindows(IntPtr hWnd, IntPtr lParam)
    {
        StringBuilder sb = new(256);
        // Obtém o texto da janela
        bool isVisible = IsWindowVisible(hWnd);
        int titleSize = GetWindowText(hWnd, sb, sb.Capacity); 
        if (!isVisible || titleSize <= 0) {
            return true;
        }
        Windows.Add(new Window(hWnd, sb.ToString()));
        return true; 
    }
}

public record Window(IntPtr Hwnd, string Title);
