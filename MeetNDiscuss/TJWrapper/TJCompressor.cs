﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace TurboJpegWrapper
{
    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// Implements compression of RGB, CMYK, grayscale images to the jpeg format
    /// </summary>
    public class TJCompressor : IDisposable
    {
        private IntPtr _compressorHandle;
        private bool _isDisposed;
        private readonly object _lock = new object();

        /// <summary>
        /// Creates new instance of <see cref="TJCompressor"/>
        /// </summary>
        /// <exception cref="TJException">
        /// Throws if internal compressor instance can not be created
        /// </exception>
        public TJCompressor()
        {
            _compressorHandle = TurboJpegImport.tjInitCompress();

            if (_compressorHandle == IntPtr.Zero)
            {
                TJUtils.GetErrorAndThrow();
            }
        }

        /// <summary>
        /// Compresses input image to the jpeg format with specified quality
        /// </summary>
        /// <param name="srcBuf">
        /// Image buffer containing RGB, grayscale, or CMYK pixels to be compressed.  
        /// This buffer is not modified.
        /// </param>
        /// <param name="stride">
        /// Bytes per line in the source image.  
        /// Normally, this should be <c>width * BytesPerPixel</c> if the image is unpadded, 
        /// or <c>TJPAD(width * BytesPerPixel</c> if each line of the image
        /// is padded to the nearest 32-bit boundary, as is the case for Windows bitmaps.  
        /// You can also be clever and use this parameter to skip lines, etc.
        /// Setting this parameter to 0 is the equivalent of setting it to
        /// <c>width * BytesPerPixel</c>.
        /// </param>
        /// <param name="width">Width (in pixels) of the source image</param>
        /// <param name="height">Height (in pixels) of the source image</param>
        /// <param name="pixelFormat">Pixel format of the source image (see <see cref="PixelFormat"/> "Pixel formats")</param>
        /// <param name="subSamp">
        /// The level of chrominance subsampling to be used when
        /// generating the JPEG image (see <see cref="TJSubsamplingOptions"/> "Chrominance subsampling options".)
        /// </param>
        /// <param name="quality">The image quality of the generated JPEG image (1 = worst, 100 = best)</param>
        /// <param name="flags">The bitwise OR of one or more of the <see cref="TJFlags"/> "flags"</param>
        /// <exception cref="TJException">
        /// Throws if compress function failed
        /// </exception>
        /// <exception cref="ObjectDisposedException">Object is disposed and can not be used anymore</exception>
        /// <exception cref="NotSupportedException"> 
        /// Some parameters' values are incompatible:
        /// <list type="bullet">
        /// <item><description>Subsampling not equals to <see cref="TJSubsamplingOptions.TJSAMP_GRAY"/> and pixel format <see cref="TJPixelFormats.TJPF_GRAY"/></description></item>
        /// </list>
        /// </exception>
        public unsafe byte[] Compress(byte[] srcBuf, int stride, int width, int height, TJPixelFormats pixelFormat, TJSubsamplingOptions subSamp, int quality, TJFlags flags)
        {
            if (_isDisposed)
                throw new ObjectDisposedException("this");

            CheckOptionsCompatibilityAndThrow(subSamp, pixelFormat);

            var buf = IntPtr.Zero;
            ulong bufSize = 0;
            try
            {
                fixed (byte* srcBufPtr = srcBuf)
                {
                    var result = TurboJpegImport.tjCompress2(
                        _compressorHandle,
                        (IntPtr)srcBufPtr,
                        width,
                        stride,
                        height,
                        (int)pixelFormat,
                        ref buf,
                        ref bufSize,
                        (int)subSamp,
                        quality,
                        (int)flags);
                    if (result == -1)
                    {
                        TJUtils.GetErrorAndThrow();
                    }
                }

                var jpegBuf = new byte[bufSize];
                // ReSharper disable once ExceptionNotDocumentedOptional
                Marshal.Copy(buf, jpegBuf, 0, (int)bufSize);
                return jpegBuf;
            }
            finally
            {
                TurboJpegImport.tjFree(buf);
            }
        }

        /// <summary>
        /// Releases resources
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {

            if (_isDisposed)
                return;

            lock (_lock)
            {
                if (_isDisposed)
                    return;

                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        private void Dispose(bool callFromUserCode)
        {
            if (callFromUserCode)
            {
                _isDisposed = true;
            }

            // If for whathever reason, the handle was not initialized correctly (e.g. an exception
            // in the constructor), we shouldn't free it either.
            if (_compressorHandle != IntPtr.Zero)
            {
                TurboJpegImport.tjDestroy(_compressorHandle);

                // Set the handle to IntPtr.Zero, to prevent double execution of this method
                // (i.e. make calling Dispose twice a safe thing to do).
                _compressorHandle = IntPtr.Zero;
            }
        }


        /// <summary>
        /// Finalizer
        /// </summary>
        ~TJCompressor()
        {
            Dispose(false);
        }

        /// <exception cref="NotSupportedException"> 
        /// Some parameters' values are incompatible:
        /// <list type="bullet">
        /// <item><description>Subsampling not equals to <see cref="TJSubsamplingOptions.TJSAMP_GRAY"/> and pixel format <see cref="TJPixelFormats.TJPF_GRAY"/></description></item>
        /// </list>
        /// </exception>
        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
        private static void CheckOptionsCompatibilityAndThrow(TJSubsamplingOptions subSamp, TJPixelFormats srcFormat)
        {
            if (srcFormat == TJPixelFormats.TJPF_GRAY && subSamp != TJSubsamplingOptions.TJSAMP_GRAY)
                throw new NotSupportedException(string.Format("Subsampling differ from {0} for pixel format {1} is not supported", TJSubsamplingOptions.TJSAMP_GRAY, TJPixelFormats.TJPF_GRAY));
        }
    }

}
