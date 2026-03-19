using System.Runtime.InteropServices;
using System.Text;

namespace Capture;

public class WindowManager {
    public WindowManager() {
        
    }

    public void Initialize() {
        
    }

    private readonly List<Window> windows = [];
    
    public List<Window> GetWindows() {
        EnumWindows(EnumTheWindows, IntPtr.Zero);
        List<Window> clone = new(windows);
        windows.Clear();
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

    
    private bool EnumTheWindows(IntPtr hWnd, IntPtr lParam)
    {
        StringBuilder sb = new(256);
        // Obtém o texto da janela
        bool isVisible = IsWindowVisible(hWnd);
        int titleSize = GetWindowText(hWnd, sb, sb.Capacity); 
        if (!isVisible || titleSize <= 0) {
            return true;
        }
        windows.Add(new Window(hWnd, sb.ToString()));
        return true; 
    }
}

public record class Window(IntPtr Hwnd, string Title);