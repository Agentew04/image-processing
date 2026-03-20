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
        Capturer capturer = new();
        capturer.Initialize();

        List<Window> windows = WindowManager.GetOpenWindows();
        Console.WriteLine("Selecione a janela para capturar");
        for (int i = 0; i < windows.Count; i++) {
            Console.WriteLine($"{i+1}. {windows[i].Title}");
        }
        Console.Write("> ");
        string input = Console.ReadLine() ?? string.Empty;
        int windowIndex = int.Parse(input)-1;
        capturer.StartCapture(windows[windowIndex], 5);
        await Task.Delay(1000);
        capturer.StopCapture();
        Console.WriteLine("Exit");
    }
}