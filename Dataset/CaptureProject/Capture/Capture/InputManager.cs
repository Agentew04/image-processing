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
                    // if (data is RawInputMouseData mouse) {
                        // OnMouse?.Invoke(new Vector2(mouse.Mouse.LastX, mouse.Mouse.LastY));
                    /*}else*/ if (data is RawInputKeyboardData keyb) {
                        OnKeyboard?.Invoke((char)keyb.Keyboard.VirutalKey, keyb.Keyboard.Flags == RawKeyboardFlags.None);
                    }
                }
                
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
            while (PeekMessage(out MSG msg, IntPtr.Zero, 0, 0, PM_REMOVE)) {
                if (msg.message == WM_HOTKEY) {
                    int id = msg.wParam.ToInt32();
                    switch (id) {
                        case HOTKEY_F7:
                            OnTogglePosition?.Invoke();
                            break;
                        case HOTKEY_F8:
                            OnCaptureToggle?.Invoke();
                            break;
                    }
                }
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            Thread.Sleep(1);
        }

        Console.WriteLine("end win32 message loop");
    }

    private INPUT[] inputArray = new INPUT[1];
    private readonly Lock inputLock = new();
    
    public void SendEvent(params ushort[] keys) {
        lock (inputLock) {
            if (inputArray.Length < keys.Length) {
                inputArray = new INPUT[keys.Length];
            }

            for(int i=0;i<keys.Length;i++) {
                ushort vkKey = keys[i];
                inputArray[0] = new INPUT {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion {
                        ki = new KEYBDINPUT {
                            wVk = vkKey,
                            dwFlags = 0
                        }
                    }
                };
                
            }

            SendInput(1, inputArray, Marshal.SizeOf<INPUT>());
        }
    }
    
    public void TypeString(string text, bool pressEnter = false)
    {
        foreach (char raw in text)
        {
            char c = raw;

            bool needsShift = char.IsUpper(c);
            c = char.ToLower(c);

            if (!KeyMap.TryGetValue(c, out var key))
                continue; // ignora caracteres não suportados

            bool useShift = needsShift || key.shift;

            if (useShift)
                KeyDownScan(LEFT_SHIFT);

            Tap(key.scan);

            if (useShift)
                KeyUpScan(LEFT_SHIFT);

            Thread.Sleep(10); // timing humanizado
        }

        if (pressEnter)
        {
            Tap(0x1C); // enter
        }
    }
    
    public void Tap(ushort scan)
    {
        KeyDownScan(scan);
        Thread.Sleep(10);
        KeyUpScan(scan);
    }
    
    void KeyUpScan(ushort scan)
    {
        SendInput(1, new[]
        {
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wScan = scan,
                        dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP
                    }
                }
            }
        }, Marshal.SizeOf<INPUT>());
    }
    
    void KeyDownScan(ushort scan)
    {
        SendInput(1, new[]
        {
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wScan = scan,
                        dwFlags = KEYEVENTF_SCANCODE
                    }
                }
            }
        }, Marshal.SizeOf<INPUT>());
    }
    
    private static readonly Dictionary<char, (ushort scan, bool shift)> KeyMap = new()
    {
        // letras
        ['a'] = (0x1E, false), ['b'] = (0x30, false), ['c'] = (0x2E, false),
        ['d'] = (0x20, false), ['e'] = (0x12, false), ['f'] = (0x21, false),
        ['g'] = (0x22, false), ['h'] = (0x23, false), ['i'] = (0x17, false),
        ['j'] = (0x24, false), ['k'] = (0x25, false), ['l'] = (0x26, false),
        ['m'] = (0x32, false), ['n'] = (0x31, false), ['o'] = (0x18, false),
        ['p'] = (0x19, false), ['q'] = (0x10, false), ['r'] = (0x13, false),
        ['s'] = (0x1F, false), ['t'] = (0x14, false), ['u'] = (0x16, false),
        ['v'] = (0x2F, false), ['w'] = (0x11, false), ['x'] = (0x2D, false),
        ['y'] = (0x15, false), ['z'] = (0x2C, false),

        // números (topo)
        ['1'] = (0x02, false), ['2'] = (0x03, false), ['3'] = (0x04, false),
        ['4'] = (0x05, false), ['5'] = (0x06, false), ['6'] = (0x07, false),
        ['7'] = (0x08, false), ['8'] = (0x09, false), ['9'] = (0x0A, false),
        ['0'] = (0x0B, false),

        // especiais
        [' '] = (0x39, false),
        ['_'] = (0x0C, true), // SHIFT + '-'
        ['-'] = (0x0C, false),
    };

    private const int LEFT_SHIFT = 0x2A;
    private const int KEYEVENTF_SCANCODE = 8;
    private const int HOTKEY_F8 = 1;
    private const int HOTKEY_F7 = 2;

    private void RegisterEvents() {
        if (!RegisterHotKey(IntPtr.Zero, HOTKEY_F8, MOD_ALT, VK_F8)) {
            Console.WriteLine("Could not register ALT F8 HOTKEY");
        }
        if (!RegisterHotKey(IntPtr.Zero, HOTKEY_F7, MOD_ALT, VK_F7)) {
            Console.WriteLine("Could not register ALT F7 HOTKEY");
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

    public event Action OnTogglePosition;
    public event Action OnCaptureToggle;
    public event Action<Vector2> OnMouse;
    public event Action<char, bool> OnKeyboard;

    private const uint MOD_ALT = 0x0001;
    private const int VK_F8 = 0x77;
    private const int VK_F7 = 0x76;
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
    
    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT {
        public int type;
        public InputUnion U;
    }
    
    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
    
    const int INPUT_MOUSE = 0;
    const int INPUT_KEYBOARD = 1;

    const uint KEYEVENTF_KEYUP = 0x0002;

    const uint MOUSEEVENTF_MOVE = 0x0001;
    const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    const uint MOUSEEVENTF_LEFTUP = 0x0004;
}