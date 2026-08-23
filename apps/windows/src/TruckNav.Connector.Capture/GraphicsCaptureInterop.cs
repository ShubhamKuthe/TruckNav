using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using WinRT;

namespace TruckNav.Connector.Capture;

internal static class GraphicsCaptureInterop
{
    private static readonly Guid GraphicsCaptureItemInterfaceId =
        new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    public static GraphicsCaptureItem CreateForWindow(nint windowHandle) =>
        Create(windowHandle, isMonitor: false);

    public static GraphicsCaptureItem CreateForMonitor(nint monitorHandle) =>
        Create(monitorHandle, isMonitor: true);

    private static GraphicsCaptureItem Create(nint handle, bool isMonitor)
    {
        if (handle == 0)
        {
            throw new ArgumentException("A native capture handle is required.", nameof(handle));
        }

        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        nint raw;
        var result = isMonitor
            ? interop.CreateForMonitor(handle, GraphicsCaptureItemInterfaceId, out raw)
            : interop.CreateForWindow(handle, GraphicsCaptureItemInterfaceId, out raw);

        Marshal.ThrowExceptionForHR(result);
        try
        {
            return GraphicsCaptureItem.FromAbi(raw);
        }
        finally
        {
            Marshal.Release(raw);
        }
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig]
        int CreateForWindow(nint window, in Guid interfaceId, out nint result);

        [PreserveSig]
        int CreateForMonitor(nint monitor, in Guid interfaceId, out nint result);
    }
}
