using System.Numerics;
using System.Runtime.InteropServices;
using HPPH;
using ScreenCapture.NET;
using SharpGen.Runtime;
using SkiaSharp;

namespace Capture;

public class Capturer : IDisposable {
    private bool initialized = false;
    private DX11ScreenCaptureService screenCaptureService;
    private List<Display> displays = [];
    private Dictionary<IntPtr, Display> handleToDisplay = [];
    private DX11ScreenCapture screenCapture;
    private CaptureZone<ColorBGRA> captureZone;
    private CancellationTokenSource cts;
    private PeriodicTimer timer;
    private Task captureTask;
    private bool isCapturePaused = false;

    public void Initialize() {
        initialized = true;
        screenCaptureService = new DX11ScreenCaptureService();
        IEnumerable<GraphicsCard> gpus = screenCaptureService.GetGraphicsCards();
        GraphicsCard gpu = gpus.FirstOrDefault(x => x.Name.Contains("NVIDIA") || x.Name.Contains("AMD") && x.Name != "Microsoft Basic Render Driver");
        if (gpu.Name == null) {
            throw new Exception("Cant find suitable GPU");
        }

        displays.AddRange(screenCaptureService.GetDisplays(gpu));
    }

    public void StartCapture(Window window, float targetFps, bool startPaused) {
        // find monitor of window 
        IntPtr monitorHandle = MonitorFromWindow(window.Hwnd, MONITOR_DEFAULTTONEAREST);
        if (!handleToDisplay.TryGetValue(monitorHandle, out Display display)) {
            handleToDisplay.Clear();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorCallback, IntPtr.Zero);
            display = handleToDisplay[monitorHandle];
        }
        
        // get monitor area
        MONITORINFOEX monitorInfo = new();
        monitorInfo.cbSize = Marshal.SizeOf(monitorInfo);
        GetMonitorInfo(monitorHandle, ref monitorInfo);
        GetWindowRect(window.Hwnd, out RECT windowRect);
        
        // se ainda eh null, deu algo mt errado
        if (display.DeviceName == null) {
            throw new Exception("Could not find display for the monitor that the selected window is at");
        }
        
        screenCapture = screenCaptureService.GetScreenCapture(display);
        int margin = 8;
        Vector2 pos = new(windowRect.left + margin - monitorInfo.rcMonitor.left,
            windowRect.top + margin - monitorInfo.rcMonitor.top);
        Vector2 size = new(windowRect.right - windowRect.left - 2*margin, windowRect.bottom - windowRect.top - 2*margin);
        captureZone = screenCapture.RegisterCaptureZone((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y);
        
        // spin up separate thread for capturing frames
        cts = new CancellationTokenSource();
        timer = new PeriodicTimer(TimeSpan.FromSeconds(1 / targetFps));
        if (startPaused) {
            isCapturePaused = true;
        }
        captureTask = Task.Run(ThreadMain);
    }

    public void PauseCapture() {
        isCapturePaused = true;
    }

    public void UnpauseCapture() {
        isCapturePaused = false;
    }

    public void StopCapture() {
        cts.Cancel();
        captureTask.GetAwaiter().GetResult();
        timer.Dispose();
    }

    private async Task ThreadMain() {
        using SKBitmap bitmap = new();
        SKImageInfo info = new(captureZone.Width, captureZone.Height, SKColorType.Bgra8888);
        while (!cts.IsCancellationRequested && await timer.WaitForNextTickAsync(cts.Token)) {
            if (isCapturePaused) {
                await Task.Delay(1);
                continue;
            }
            screenCapture.CaptureScreen();
            using (captureZone.Lock()) {
                ref byte buffer = ref MemoryMarshal.GetReference(captureZone.RawBuffer);
                unsafe {
                    fixed (byte* ptr = &buffer) {
                        bitmap.InstallPixels(info, (IntPtr)ptr);
                    }
                }
                using SKData data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100);
                string path = $"./img-{Random.Shared.Next()}.jpg";
                await using FileStream fs = File.Create(path);
                data.SaveTo(fs);
                Console.WriteLine($"Captured screen to {path}");
            }
        }
    }

    public void Dispose() {
        if (!initialized || cts is null) {
            return;
        }

        if (!cts.IsCancellationRequested) {
            StopCapture();
        }
        screenCaptureService.Dispose();
        screenCapture.Dispose();
    }

    private bool MonitorCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT rect, IntPtr data) {
        MONITORINFOEX info = new();
        info.cbSize = Marshal.SizeOf(info);
        GetMonitorInfo(hMonitor, ref info);
        string name = info.szDevice;
        Display targetDisplay = displays.FirstOrDefault(x => x.DeviceName == name);
        if (targetDisplay.DeviceName == null) {
            Console.WriteLine("Could not map monitor to display. mapping to empty!");
        }
        handleToDisplay[hMonitor] = targetDisplay;
        return true;
    }

    const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    public delegate bool MonitorEnumProc(
        IntPtr hMonitor,
        IntPtr hdcMonitor,
        ref RECT lprcMonitor,
        IntPtr dwData);

    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }
    
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);
    
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
}