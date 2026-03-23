using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;
using ABI.Windows.Graphics.Capture;
using Windows.Foundation.Metadata;
using HPPH;
using ScreenCapture.NET;
using SkiaSharp;

namespace Capture;

class Program {
    
    [STAThread]
    static async Task Main(string[] args) {
        using Capturer capturer = new();
        using InputManager inputManager = new();
        using Detector detector = new();
        capturer.Initialize();
        inputManager.Initialize();
        detector.Initialize();

        List<Window> windows = WindowManager.GetOpenWindows();
        Console.WriteLine("Selecione a janela para capturar");
        for (int i = 0; i < windows.Count; i++) {
            Console.WriteLine($"{i+1}. {windows[i].Title}");
        }
        Console.Write("> ");
        string input = Console.ReadLine() ?? string.Empty;
        int windowIndex = int.Parse(input)-1;
        
        inputManager.StartMessageLoop();

        bool capturing = false;
        inputManager.OnCaptureToggle += () => {
            capturing = !capturing;
            if (capturing) {
                Console.WriteLine("Unpaused capture");
                capturer.UnpauseCapture();
            }
            else {
                Console.WriteLine("Paused capture");
                capturer.PauseCapture();
            }
        };
        capturer.OnCapture += bmp => {
            using SKData data = bmp.Encode(SKEncodedImageFormat.Jpeg, 100);
            string name = DateTime.Now.Ticks.ToString();
            string path = $"./captures/{name}-frame.jpg";
            using FileStream fs = File.Create(path);
            data.SaveTo(fs);
            Console.WriteLine($"Captured screen to {path}");
            using SKImage? copy = SKImage.FromBitmap(bmp.Copy());
            detector.Detect(copy, name);
        };
        
        Console.WriteLine("System Ready");
        
        capturer.StartCapture(windows[windowIndex], 5, true);

        Console.WriteLine("Press ENTER to stop");
        Console.ReadLine();
        
        inputManager.StopMessageLoop();
        capturer.StopCapture();
        Console.WriteLine("Exit");
    }
}