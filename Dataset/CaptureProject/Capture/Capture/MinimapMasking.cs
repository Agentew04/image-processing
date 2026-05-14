using SkiaSharp;

namespace Capture;

public static class MinimapMasking {

    public static SKBitmap Process(SKBitmap input) {
        int w = input.Width;
        int h = input.Height;
        SKBitmap output = new(w, h, SKColorType.Gray8, SKAlphaType.Opaque);

        IntPtr inPtr = input.GetPixels();
        IntPtr outPtr = output.GetPixels();
        
        int inStride = input.RowBytes;
        int outStride = output.RowBytes;

        // get/set pixel -> 300ms
        // byte* -> 105us, loucura de otimizacao, obg gpt
        
        unsafe {
            byte* inBytes = (byte*)inPtr;
            byte* outBytes = (byte*)outPtr;

            for (int y = 0; y < h; y++) {
                byte* inRow = inBytes + y * inStride;
                byte* outRow = outBytes + y * outStride;

                for (int x = 0; x < w; x++) {
                    // SKColor = 4 bytes (BGRA)
                    byte b = inRow[x * 4 + 0];
                    byte g = inRow[x * 4 + 1];
                    byte r = inRow[x * 4 + 2];
                    bool gray = r <= 65 && g <= 65 && b <= 65;
                    outRow[x] = gray ? (byte)255 : (byte)0;
                }
            }
        }

        return output;
    }
}