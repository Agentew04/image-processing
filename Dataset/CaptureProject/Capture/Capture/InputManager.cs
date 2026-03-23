using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Windows.UI.Input;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;

namespace Capture;

public class InputManager : IDisposable {
    private CancellationTokenSource? cts;
    private Task? messageLoopTask;
    private IntPtr hiddenWindowHandle;

    public void Initialize() {
        RawInputDevice[] devices = RawInputDevice.GetDevices();
        Console.WriteLine($"Detected {devices.OfType<RawInputKeyboard>().Count()} keyboards and {devices.OfType<RawInputMouse>().Count()} mice");
    }

    public void StartMessageLoop() {
        cts = new CancellationTokenSource();
        messageLoopTask = Task.Run(ThreadMain);
    }

    public void StopMessageLoop() {
        cts?.Cancel();
        messageLoopTask?.GetAwaiter().GetResult();
    }

    private void ThreadMain() {
        RegisterEvents();
        CreateHiddenWindow();

        while (!cts.IsCancellationRequested) {
            while (PeekMessage(out MSG msg, hiddenWindowHandle, 0, 0, PM_REMOVE)) {
                if (msg.message == WM_INPUT) {
                    // processa input aqui
                    var data = RawInputData.FromHandle(msg.lParam);
                    if (data is RawInputMouseData mouse) {
                        OnMouse?.Invoke(new Vector2(mouse.Mouse.LastX, mouse.Mouse.LastY));
                    }else if (data is RawInputKeyboardData keyb) {
                        OnKeyboard?.Invoke((char)keyb.Keyboard.VirutalKey, keyb.Keyboard.Flags == RawKeyboardFlags.None);
                    }
                }
                
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
            while (PeekMessage(out MSG msg, IntPtr.Zero, 0, 0, PM_REMOVE)) {
                if (msg.message == WM_HOTKEY) {
                    OnCaptureToggle?.Invoke();
                }
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            Thread.Sleep(1);
        }

        Console.WriteLine("end win32 message loop");
    }

    private void RegisterEvents() {
        if (!RegisterHotKey(IntPtr.Zero, 1, MOD_ALT, VK_F8)) {
            Console.WriteLine("Could not register ALT F8 HOTKEY");
        }
    }

    public void Dispose() {
        if (!(cts?.IsCancellationRequested ?? false)) {
            StopMessageLoop();
        }

        DestroyWindow(hiddenWindowHandle);
        UnregisterClass("MyHiddenWindow", IntPtr.Zero);
    }

    private void CreateHiddenWindow() {
        string className = "MyHiddenWindow";
        WNDCLASS wc = new() {
            lpszClassName = Marshal.StringToHGlobalUni(className),
            lpfnWndProc = DefWindowProc
        };
    
        ushort classAtom = RegisterClassW(ref wc);
        if (classAtom == 0) {
            Win32Exception ex = new();
            Console.WriteLine("Error registering class: " + ex.Message);
            return;
        }
        const uint WS_EX_NOACTIVATE = 0x08000000;
        const uint WS_POPUP = 0x80000000;
        
        hiddenWindowHandle = CreateWindowExW(WS_EX_NOACTIVATE, className, "", WS_POPUP,
            0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        Console.WriteLine($"Create hidden Window: {hiddenWindowHandle}");
        
        RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard, 
            RawInputDeviceFlags.ExInputSink | RawInputDeviceFlags.NoLegacy, hiddenWindowHandle);
        RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, 
            RawInputDeviceFlags.ExInputSink | RawInputDeviceFlags.NoLegacy, hiddenWindowHandle);
    }

    public event Action OnCaptureToggle;
    public event Action<Vector2> OnMouse;
    public event Action<char, bool> OnKeyboard;

    private const uint MOD_ALT = 0x0001;
    private const int VK_F8 = 0x77;
    private const int WM_HOTKEY = 0x0312;
    private const int WM_INPUT = 0x00ff;

    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MSG {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    const uint PM_NOREMOVE = 0x0000;
    const uint PM_REMOVE = 0x0001;

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax,
        uint wRemoveMsg);
    
    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);
    
    [StructLayout(LayoutKind.Sequential)]
    struct WNDCLASS
    {
        public uint style;
        public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public nint lpszClassName;
    }
    
    [DllImport("user32.dll", SetLastError = true)]
    static extern ushort RegisterClassW([In] ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr CreateWindowExW(uint dwExStyle, [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
    
    delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    
    [DllImport("user32.dll")]
    public static extern bool DestroyWindow(IntPtr hWnd);
    
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);
    
}