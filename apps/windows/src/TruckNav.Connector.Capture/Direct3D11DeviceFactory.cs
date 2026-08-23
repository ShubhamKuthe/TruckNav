using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace TruckNav.Connector.Capture;

internal static class Direct3D11DeviceFactory
{
    private static readonly Guid DxgiDeviceInterfaceId =
        new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");

    public static IDirect3DDevice Create()
    {
        var result = D3D11CreateDevice(
            0,
            D3DDriverType.Hardware,
            0,
            D3D11CreateDeviceBgraSupport,
            0,
            0,
            D3D11SdkVersion,
            out var d3dDevice,
            out _,
            out var immediateContext);

        if (result < 0)
        {
            result = D3D11CreateDevice(
                0,
                D3DDriverType.Warp,
                0,
                D3D11CreateDeviceBgraSupport,
                0,
                0,
                D3D11SdkVersion,
                out d3dDevice,
                out _,
                out immediateContext);
        }

        Marshal.ThrowExceptionForHR(result);

        nint dxgiDevice = 0;
        nint inspectableDevice = 0;
        try
        {
            var interfaceId = DxgiDeviceInterfaceId;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d3dDevice, ref interfaceId, out dxgiDevice));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out inspectableDevice));
            return MarshalInterface<IDirect3DDevice>.FromAbi(inspectableDevice);
        }
        finally
        {
            if (inspectableDevice != 0)
            {
                Marshal.Release(inspectableDevice);
            }

            if (dxgiDevice != 0)
            {
                Marshal.Release(dxgiDevice);
            }

            if (immediateContext != 0)
            {
                Marshal.Release(immediateContext);
            }

            if (d3dDevice != 0)
            {
                Marshal.Release(d3dDevice);
            }
        }
    }

    private const uint D3D11SdkVersion = 7;
    private const uint D3D11CreateDeviceBgraSupport = 0x20;

    private enum D3DDriverType : uint
    {
        Hardware = 1,
        Warp = 5
    }

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(
        nint adapter,
        D3DDriverType driverType,
        nint software,
        uint flags,
        nint featureLevels,
        uint featureLevelCount,
        uint sdkVersion,
        out nint device,
        out uint featureLevel,
        out nint immediateContext);

    [DllImport("d3d11.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(
        nint dxgiDevice,
        out nint graphicsDevice);
}
