using System.Runtime.InteropServices;

namespace Capture;

[ComImport]
[Guid("3628e81b-3cac-4c60-b7f4-23ce0e0c3356")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IGraphicsCaptureItemInterop
{
    public void CreateForWindow(
        IntPtr window,
        ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out object result);
}