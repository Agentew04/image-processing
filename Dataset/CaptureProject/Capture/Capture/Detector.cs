using System.Diagnostics;
using SkiaSharp;
using YoloDotNet;
using YoloDotNet.ExecutionProvider.Cpu;
using YoloDotNet.Extensions;
using YoloDotNet.Models;

namespace Capture;

public class Detector : IDisposable{

    private Yolo yolo;
    private Stopwatch sw = new();
    
    public Detector() {
        
    }

    public void Initialize() {
        try {
            yolo = new Yolo(new YoloOptions {
                ExecutionProvider = new CpuExecutionProvider("yolov8s_cs2.onnx")
            });
        }
        catch (Exception e) {
            Console.WriteLine("Error initializing yolo: " + e.Message);
            throw;
        }
    }
    
    public void Detect(SKImage image, string name) {
        sw.Restart();
        List<ObjectDetection> results = yolo.RunObjectDetection(image, confidence: 0.25, iou: 0.7)
            .ToList(); 
        sw.Stop();
        Console.WriteLine($"Detected: ({sw.ElapsedMilliseconds}ms)");
        foreach (ObjectDetection result in results) {
            Console.Write("  - ");
            Console.WriteLine($"({result.Label.Index}, {result.Label.Name}) - {result.BoundingBox}");
        }

        using SKBitmap drawn = image.Draw(results);
        drawn.Save($"./captures/{name}-yolo.jpg");
    }

    public void Dispose() {
        yolo.Dispose();
    }
}