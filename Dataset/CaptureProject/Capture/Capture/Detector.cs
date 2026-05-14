using SkiaSharp;
using YoloDotNet;
// using YoloDotNet.Exceptions;
// using YoloDotNet.ExecutionProvider.Cpu;
// using YoloDotNet.ExecutionProvider.Cuda;
using YoloDotNet.ExecutionProvider.DirectML;
using YoloDotNet.Extensions;
// using YoloDotNet.ExecutionProvider.OpenVino;
using YoloDotNet.Models;

namespace Capture;

public class Detector : IDisposable{

    private Yolo yolo;

    private enum ExecutionProvider {
        Cuda,
        DirectMl,
        OpenVino,
        Cpu
    }
    
    public void Initialize() {
        // if (TryYolo(ExecutionProvider.Cuda)) {
        //     Console.WriteLine("Using CUDA as Yolo Execution Provider");
        //     return;
        // }

        if (OperatingSystem.IsWindows() && TryYolo(ExecutionProvider.DirectMl)) {
            Console.WriteLine("Using DirectML as Yolo Execution Provider");
            return;
        }
        
        if (TryYolo(ExecutionProvider.OpenVino)) {
            Console.WriteLine("Using OpenVino as Yolo Execution Provider");
            return;
        }

        if (TryYolo(ExecutionProvider.Cpu)) {
            Console.WriteLine("Using CPU as Yolo Execution Provider");
            return;
        }
        
        Console.WriteLine("Could not find any suitable execution provider for YOLO");
    }

    private bool TryYolo(ExecutionProvider provider) {
        const string model = "yolov8s_cs2.onnx";
        try {
            yolo = new Yolo(new YoloOptions {
                
                ExecutionProvider = provider switch {
                //     // ExecutionProvider.Cuda => new CudaExecutionProvider(model),
                ExecutionProvider.DirectMl => new DirectMLExecutionProvider(model),
                //     // ExecutionProvider.OpenVino => new OpenVinoExecutionProvider(model),
                //     // ExecutionProvider.Cpu => new CpuExecutionProvider(model)
                _ => null!
                }
            });
            return true;
        }
        catch (Exception e) {
            Console.WriteLine($"Error trying {provider} execution provider for yolo ({e.GetType().Name}): {e.Message}");
            return false;
        }
    }
    
    public List<ObjectDetection> Detect(SKImage image) {
        ChromeTraceScope scope = new("Detect", "Yolo Detection");
        List<ObjectDetection> results = yolo.RunObjectDetection(image, confidence: 0.25, iou: 0.7);
        scope.Dispose();
        long ticks = DateTime.UtcNow.Ticks;
        // image.Save($"captures/{ticks}-cs.jpg");
        SKBitmap drawn = image.Draw(results);
        // drawn.Save($"captures/{ticks}-yolo.jpg");
        return results;
    }

    public void Dispose() {
        yolo?.Dispose();
    }
}