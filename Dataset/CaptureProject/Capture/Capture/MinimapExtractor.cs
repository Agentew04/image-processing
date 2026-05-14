using System.Diagnostics;
using Windows.ApplicationModel.Calls;
using SkiaSharp;
using Vortice.Mathematics;
using YoloDotNet.Extensions;

namespace Capture;

public class MinimapExtractor : IDisposable {
    
    public void Initialize() {
        
    }

    public float[] ExtractMinimap(SKImage frame) {
        SKImage minimap = frame.Subset(new SKRectI(28, 28, 272, 272));
        using SKBitmap bitmap = SKBitmap.FromImage(minimap);

        long ticks = DateTime.UtcNow.Ticks;
        // minimap.Save($"captures/{ticks}-minimap.jpg");

        ChromeTraceScope minimapMask = new("Minimap", "Mask");
        using SKBitmap edges = MinimapMasking.Process(bitmap);
        minimapMask.Dispose();
        // edges.Save($"captures/{ticks}-mask.jpg");

        ChromeTraceScope paintPlayer = new("Minimap", "Paint Player");
        using (SKCanvas canvas = new(edges)) {
            using SKPaint paint = new();
            paint.Color = SKColors.Black;
            paint.IsAntialias = false;
            canvas.DrawRect(114, 110, 14, 18, paint);
        }
        paintPlayer.Dispose();

        ChromeTraceScope raycastTrace = new("Minimap", "Raycast");
        float[] rays = RaycastMinimap(edges);
        raycastTrace.Dispose();
        // edges.Save($"captures/{ticks}-raycast.jpg");

        return rays;
    }

    private float[] RaycastMinimap(SKBitmap minimap) {
        const int rayCount = 64;
        float maxDist = 200;
        // using SKCanvas canvas = new(minimap);
        // SKPaint paint = new() {
        //     Color = SKColors.Red,
        //     StrokeWidth = 1
        // };

        float[] rays = new float[rayCount];
        for (int i = 0; i < rayCount; i++) {
            float angle = -MathF.PI / 2 + i * (MathF.PI / (rayCount - 1));
            rays[i] = CastRay(minimap, 121, 121, angle, maxDist);
        }

        // for (int i = 0; i < rayCount; i++)
        // {
        //     float angle = -MathF.PI / 2 + i * (MathF.PI / (rayCount - 1));
        //
        //     float dx = MathF.Sin(angle);
        //     float dy = -MathF.Cos(angle);
        //
        //     float dist = rays[i];
        //
        //     canvas.DrawLine(
        //         121, 121,
        //         121 + dx * dist,
        //         121 + dy * dist,
        //         paint);
        // }
        
        for (int i = 0; i < rayCount; i++)
        {
            rays[i] /= maxDist; // maxDist
        }
        
        return rays;
        float CastRay(SKBitmap edges, float ox, float oy, float angle, float maxDist)
        {
            float dx = MathF.Sin(angle);   // ⚠️ sim, sin aqui
            float dy = -MathF.Cos(angle);  // porque "cima" é -Y

            for (float t = 0; t < maxDist; t += 1f)
            {
                int x = (int)(ox + dx * t);
                int y = (int)(oy + dy * t);

                if (x < 0 || y < 0 || x >= edges.Width || y >= edges.Height)
                    return maxDist;

                if (edges.GetPixel(x, y).Red > 0) // bateu numa borda
                    return t;
            }

            return maxDist;
        }
    }

    public void Dispose() {
        
    }
}