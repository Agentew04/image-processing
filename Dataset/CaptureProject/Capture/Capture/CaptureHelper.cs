using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using ABI.Windows.Graphics.Capture;

namespace Capture;

public class CaptureHelper {
    public static GraphicsCaptureItem CreateItemForWindow(IntPtr hwnd)
    {
        var factory = WinRT.ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");

        var interop = factory.As<IGraphicsCaptureItemInterop>();

        Guid iid = typeof(GraphicsCaptureItem).GUID;

        interop.Vftbl.CreateForWindow(hwnd, ref iid, out object result);

        return (GraphicsCaptureItem)result;
    }
}
