using System.Runtime.InteropServices;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;
using Windows.Graphics.Capture;
using Windows.Graphics.Imaging;
using WinRT;

namespace TruckNav.Connector.Capture;

internal static class SoftwareBitmapFrameConverter
{
    public static async Task<VideoFrame> ConvertAsync(
        Direct3D11CaptureFrame captureFrame,
        long sequenceNumber,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var copied = await SoftwareBitmap.CreateCopyFromSurfaceAsync(
            captureFrame.Surface,
            BitmapAlphaMode.Premultiplied);

        SoftwareBitmap bitmap = copied;
        SoftwareBitmap? converted = null;
        if (copied.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
            copied.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
        {
            converted = SoftwareBitmap.Convert(
                copied,
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);
            bitmap = converted;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var width = bitmap.PixelWidth;
            var height = bitmap.PixelHeight;
            var targetStride = checked(width * 4);
            var data = new byte[checked(targetStride * height)];

            using var bitmapBuffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read);
            var plane = bitmapBuffer.GetPlaneDescription(0);
            using var reference = bitmapBuffer.CreateReference();
            var byteAccess = reference.As<IMemoryBufferByteAccess>();
            byteAccess.GetBuffer(out var source, out var capacity);

            var requiredBytes = checked(plane.StartIndex + plane.Stride * plane.Height);
            if (source == 0 || requiredBytes > capacity)
            {
                throw new InvalidOperationException("The captured software bitmap buffer is incomplete.");
            }

            for (var row = 0; row < height; row++)
            {
                Marshal.Copy(
                    source + plane.StartIndex + row * plane.Stride,
                    data,
                    row * targetStride,
                    targetStride);
            }

            return new VideoFrame(
                width,
                height,
                targetStride,
                CapturePixelFormat.Bgra8Premultiplied,
                DateTimeOffset.UtcNow,
                sequenceNumber,
                data);
        }
        finally
        {
            converted?.Dispose();
        }
    }

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMemoryBufferByteAccess
    {
        void GetBuffer(out nint buffer, out uint capacity);
    }
}
