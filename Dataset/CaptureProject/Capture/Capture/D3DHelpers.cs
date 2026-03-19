using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;
using SharpGen.Runtime;
using Vortice.DXGI;

namespace Capture;

public class D3DHelpers {
    [DllImport("d3d11.dll", ExactSpelling = true)]
    public static extern Result CreateDirect3D11DeviceFromDXGIDevice(
        IntPtr dxgiDevice,
        out IntPtr graphicsDevice);
    
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
}