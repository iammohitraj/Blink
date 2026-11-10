// Blink · mohit

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Blink;

/// <summary>
/// Decodes images using the Windows Imaging Component (WIC), adding native support
/// for modern and advanced formats like WebP, TIFF, and HEIF without third-party dependencies.
/// </summary>
internal static class WicDecoder
{
    private static readonly Guid WicFormat32bppPArgb = new("6fddc324-4e03-4bfe-b185-3d77768dc923");

    [DllImport("windowscodecs.dll", EntryPoint = "WICCreateImagingFactory_Proxy")]
    private static extern int WICCreateImagingFactory_Proxy(uint sdkVersion, out nint ppIFactory);

    [ComImport]
    [Guid("ec5ec888-8119-4f1d-b174-a62c4164c390")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICImagingFactory
    {
        int CreateDecoderFromFilename(
            [MarshalAs(UnmanagedType.LPWStr)] string wzFilename,
            ref Guid pguidVendor,
            uint dwDesiredAccess,
            uint metadataOptions,
            out IWICBitmapDecoder ppIDecoder);

        int CreateDecoderFromStream(
            nint pIStream,
            ref Guid pguidVendor,
            uint metadataOptions,
            out IWICBitmapDecoder ppIDecoder);

        int CreateDecoderFromFileHandle();
        int CreateComponentInfo();
        int CreateDecoder();
        int CreateEncoder();
        int CreatePalette();
        int CreateFormatConverter(out IWICFormatConverter ppIFormatConverter);
    }

    [ComImport]
    [Guid("9EDDE9FF-3DE3-4311-A862-F50E249F6F14")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICBitmapDecoder
    {
        int QueryCapability();
        int Initialize();
        int GetContainerFormat();
        int GetDecoderInfo();
        int CopyPalette();
        int GetMetadataQueryReader();
        int GetPreview();
        int GetColorContexts();
        int GetThumbnail();
        int GetFrameCount(out uint pCount);
        int GetFrame(uint index, out IWICBitmapFrameDecode ppIBitmapFrame);
    }

    [ComImport]
    [Guid("3B16E111-71E0-464A-A8E2-2A8E5C49A7E0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICBitmapSource
    {
        int GetSize(out uint puiWidth, out uint puiHeight);
        int GetPixelFormat(out Guid pPixelFormat);
        int GetResolution(out double pDpiX, out double pDpiY);
        int CopyPalette();
        int CopyPixels(nint prc, uint cbStride, uint cbBufferSize, nint pbBuffer);
    }

    [ComImport]
    [Guid("3B16E111-71E0-464A-A8E2-2A8E5C49A7E0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICBitmapFrameDecode : IWICBitmapSource
    {
    }

    [ComImport]
    [Guid("00000301-a8f2-4877-ba0a-fd2b6645fb94")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICFormatConverter : IWICBitmapSource
    {
        int Initialize(
            IWICBitmapSource pISource,
            ref Guid dstFormat,
            uint dither,
            nint pIPalette,
            double alphaThresholdPercent,
            uint paletteTranslate);
    }

    public static Bitmap? TryDecode(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            Guid vendor = Guid.Empty;
            int hr = WICCreateImagingFactory_Proxy(0x0236, out nint factoryPtr);
            if (hr != 0 || factoryPtr == nint.Zero)
            {
                return null;
            }

            IWICImagingFactory factory = (IWICImagingFactory)Marshal.GetObjectForIUnknown(factoryPtr);
            Marshal.Release(factoryPtr);

            hr = factory.CreateDecoderFromFilename(path, ref vendor, 0x80000000, 0, out IWICBitmapDecoder decoder);
            if (hr != 0 || decoder is null)
            {
                return null;
            }

            hr = decoder.GetFrame(0, out IWICBitmapFrameDecode frame);
            if (hr != 0 || frame is null)
            {
                return null;
            }

            hr = factory.CreateFormatConverter(out IWICFormatConverter converter);
            if (hr != 0 || converter is null)
            {
                return null;
            }

            Guid targetFormat = WicFormat32bppPArgb;
            hr = converter.Initialize(frame, ref targetFormat, 0, nint.Zero, 0.0, 0);
            if (hr != 0)
            {
                return null;
            }

            converter.GetSize(out uint width, out uint height);
            if (width == 0 || height == 0)
            {
                return null;
            }

            Bitmap bitmap = new((int)width, (int)height, PixelFormat.Format32bppPArgb);
            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, (int)width, (int)height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppPArgb);

            try
            {
                uint stride = (uint)data.Stride;
                uint bufferSize = stride * height;
                converter.CopyPixels(nint.Zero, stride, bufferSize, data.Scan0);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}

