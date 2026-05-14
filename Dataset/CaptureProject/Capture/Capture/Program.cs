using System.Numerics;
using SkiaSharp;
using YoloDotNet.Models;

namespace Capture;

public static class Program {

    private static Dictionary<char, bool> keyStates = [];
    private static readonly char[] watchedKeys = [
        'W', 'A', 'S', 'D'
    ];
    
    [STAThread]
    public static async Task Main(string[] args) {
        using Capturer capturer = new();
        using InputManager inputManager = new();
        using Detector detector = new();
        using MinimapExtractor minimapExtractor = new();
        using DebugUiReader debugReader = new();
        capturer.Initialize();
        inputManager.Initialize();
        detector.Initialize();
        debugReader.Initialize();

        List<Window> windows = WindowManager.GetOpenWindows();
        Window? cs = windows.FirstOrDefault(x => x.Title == "Counter-Strike 2");
        if (cs is null) {
            // could not find automagically, prompt user
            Console.WriteLine("Selecione a janela para capturar");
            for (int i = 0; i < windows.Count; i++) {
                Console.WriteLine($"{i+1}. {windows[i].Title}");
            }
            Console.Write("> ");
            string input = Console.ReadLine() ?? string.Empty;
            int windowIndex = int.Parse(input)-1;
            cs = windows[windowIndex];
        }
        else {
            Console.WriteLine("Found CS2 window");
        }
        
        inputManager.StartMessageLoop();

        bool capturing = false;
        bool positionShowing = false;
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
        inputManager.OnTogglePosition += () => {
            positionShowing = !positionShowing;
            if (positionShowing) {
                Console.WriteLine("Show position");
                inputManager.TypeString("p");
                // inputManager.Tap(0x29);
                Thread.Sleep(150);
                inputManager.TypeString("sv_cheats true", true);
                Thread.Sleep(50);
                inputManager.TypeString("cl_showpos 1", true);
                Thread.Sleep(50);
                inputManager.Tap(0x1);
            }
            else {
                Console.WriteLine("Hide Position");
                // inputManager.Tap(0x29);
                inputManager.TypeString("p");
                Thread.Sleep(150);
                inputManager.TypeString("cl_showpos 0", true);
                Thread.Sleep(50);
                inputManager.TypeString("sv_cheats false", true);
                Thread.Sleep(50);
                inputManager.Tap(0x1);
            }
        };
        
        inputManager.OnKeyboard += (key, pressed) => {
            if (!watchedKeys.Contains(key)) {
                // Console.WriteLine("ignored key: " + key);
                return;
            }
            keyStates[key] = pressed;
        };
        capturer.OnCapture += bmp => {
            SKColor helmetPixel = bmp.GetPixel(635, 1016);
            int tolerance = 16;
            if (Math.Abs(helmetPixel.Red - 0xec) > tolerance 
                || Math.Abs(helmetPixel.Green - 0xc0) > tolerance 
                || Math.Abs(helmetPixel.Blue - 0x59) > tolerance) {
                // player is dead. helmet no longer in the hud
                Console.WriteLine("Player dead");
                return;
            }

            (Vector3 position, Vector2 orientation) = debugReader.Read(SKImage.FromBitmap(bmp));
            List<ObjectDetection> objs = detector.Detect(SKImage.FromBitmap(bmp));
            float[] rays = minimapExtractor.ExtractMinimap(SKImage.FromBitmap(bmp));
            
            // correlate all async key & mouse events received in the frame time
            // keyStates.TryGetValue('W', out bool w);
            // keyStates.TryGetValue('A', out bool a);
            // keyStates.TryGetValue('S', out bool s);
            // keyStates.TryGetValue('D', out bool d);
            
            // dispatch assembled frame
                
            // reset input matching data
        };
        
        Console.WriteLine("System Ready");
        
        capturer.StartCapture(cs, 1, true);

        Console.WriteLine("Press ENTER to stop");
        Console.ReadLine();
        
        inputManager.StopMessageLoop();
        capturer.StopCapture();
        Console.WriteLine("Exit");
    }
}