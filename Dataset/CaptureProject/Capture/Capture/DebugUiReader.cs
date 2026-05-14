using System.Numerics;
using System.Reflection;
using SkiaSharp;
using YoloDotNet.Extensions;

namespace Capture;

public class DebugUiReader : IDisposable {

    private readonly Dictionary<char, SKBitmap> patterns = [];

    private readonly char[] allowedChars = [
        '-','0','1','2','3','4','5','6','7','8','9','.'
    ];
    
    public void Initialize() {
        foreach (char c in allowedChars) {
            Assembly asm = Assembly.GetExecutingAssembly();
            using Stream? s = asm.GetManifestResourceStream($"Capture.digits.{c}.png");
            if (s is null) {
                Console.WriteLine($"Error loading pattern for digit {c}");
                continue;
            }
            patterns[c] = SKBitmap.FromImage(SKImage.FromEncodedData(s));
        }
    }
    
    public (Vector3 position, Vector2 orientation) Read(SKImage image) {
        // max 26 chars
        // each char is 13x17 pixels
        // 3 pixels between chars
        const int maxLetters = 26;
        const int spacing = 4;
        const int width = 13;
        const int height = 17;

        SKImage positionImage = image.Subset(new SKRectI(106, 265, 106 + (width * maxLetters) + (spacing * (maxLetters-1)), 265 + height));
        long ticks = DateTime.UtcNow.Ticks;
        positionImage.Save($"captures/{ticks}-letters.jpg");
        Span<char> str = stackalloc char[maxLetters];
        for (int i = 0; i < maxLetters; i++) {
            SKImage letter = positionImage.Subset(new SKRectI(i * spacing + i * width, 0, i * spacing + (i + 1) * width, height));
            Console.WriteLine($"{ticks} - {i}:");
            str[i] = Recognize(letter);
        }

        Console.Write($"Str {ticks}: ");
        Console.WriteLine(str);
        return (default, default);
    }

    private char Recognize(SKImage image) {
        List<(char, float)> values = [];
        foreach (KeyValuePair<char, SKBitmap> kvp in patterns) {
            using SKBitmap bmp = SKBitmap.FromImage(image); // allocate
            float similarity = Similarity(bmp, kvp.Value);
            values.Add((kvp.Key, similarity));
        }

        IOrderedEnumerable<(char, float)> ordered = values.OrderBy(x => x.Item2);
        foreach (var kvp in ordered) {
            Console.WriteLine($"  {kvp.Item1}: {kvp.Item2}");
        }

        (char, float) best = values.MaxBy(x => x.Item2);
        if (best.Item2 < 0.5) {
            return ' ';
        }

        return best.Item1;
    }

    private float Similarity(SKBitmap image, SKBitmap pattern) {
        int w = image.Width;
        int h = image.Height;

        int missing = 0;
        int extra = 0;
        int relevant = 0;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool tOn = pattern.GetPixel(x, y).Red > 128;
                image.GetPixel(x, y).ToHsv(out _, out _, out float value);

                if (tOn)
                {
                    relevant++;
                    if (value < 0.9) missing++;
                }
                else
                {
                    if (iOn) extra++;
                }
            }
        }

        return (missing + extra * 0.5f) / (relevant + 1);
    }
    
    public void Dispose() {
    }
}