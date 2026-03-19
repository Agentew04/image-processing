using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;
using ABI.Windows.Graphics.Capture;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Foundation.Metadata;

namespace Capture;

class Program {
    
    [STAThread]
    static void Main(string[] args) {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        
        Result result = D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            null,
            out ID3D11Device d3dDevice,
            out ID3D11DeviceContext _);
        if (result.Failure) {
            Console.WriteLine("failed to get device");
            return;
        }

        Console.WriteLine("Got device");
        
        IDXGIDevice dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>();
        IntPtr unknown;
        result = D3DHelpers.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out unknown);
        if (result.Failure) {
            Console.WriteLine("Failed creating winrt device");
            return;
        }
        IDirect3DDevice winrtDevice =
            WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(unknown);
        if (winrtDevice is null) {
            Console.WriteLine("Failed to get WinRT device");
            Marshal.Release(unknown);
            return;
        }
        
        bool supported =
            ApiInformation.IsTypePresent("Windows.Graphics.Capture.GraphicsCaptureSession");
        Console.WriteLine($"GraphicsCapture supported: {supported}");
        if (!supported) {
            Console.WriteLine("graphicscapture not supported");
            return;
        }
        
        IntPtr hwnd = D3DHelpers.GetForegroundWindow();
        Console.WriteLine("Foreground HWND: " + hwnd);
        
        GraphicsCaptureItem item = CaptureHelper.CreateItemForWindow(hwnd);
        // Console.WriteLine($"Capture size {item .Size.Width}x{item.Size.Height}");
        
        
        
        
        
        Marshal.Release(unknown);
    }
}