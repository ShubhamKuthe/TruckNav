using System.Runtime.InteropServices;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Capture;

public sealed class WindowsMonitorProvider : IWindowsMonitorProvider
{
    public IReadOnlyList<CaptureSource> GetMonitorSources()
    {
        var sources = new List<CaptureSource>();
        var monitorIndex = 0;

        EnumDisplayMonitors(
            0,
            0,
            (monitor, _, _, _) =>
            {
                var info = new MonitorInfoEx
                {
                    Size = Marshal.SizeOf<MonitorInfoEx>()
                };

                if (!GetMonitorInfo(monitor, ref info))
                {
                    return true;
                }

                monitorIndex++;
                var width = info.Monitor.Right - info.Monitor.Left;
                var height = info.Monitor.Bottom - info.Monitor.Top;
                var isPrimary = (info.Flags & MonitorInfoPrimary) != 0;
                sources.Add(new CaptureSource(
                    $"monitor:{info.DeviceName}",
                    $"Monitor {monitorIndex} - {width} x {height}",
                    CaptureSourceType.Monitor,
                    width,
                    height,
                    monitor,
                    Left: info.Monitor.Left,
                    Top: info.Monitor.Top,
                    IsPrimary: isPrimary));
                return true;
            },
            0);

        return sources
            .OrderByDescending(source => source.IsPrimary)
            .ThenBy(source => source.Left)
            .ThenBy(source => source.Top)
            .ToArray();
    }

    private const uint MonitorInfoPrimary = 1;

    private delegate bool MonitorEnumProc(nint monitor, nint deviceContext, nint monitorRectangle, nint data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        nint deviceContext,
        nint clipRectangle,
        MonitorEnumProc callback,
        nint data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfoEx monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRectangle Monitor;
        public NativeRectangle WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }
}
