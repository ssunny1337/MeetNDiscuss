using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using TurboJpegWrapper;
using System.Windows.Threading;
using static AForge.Imaging.Filters.HitAndMiss;

namespace MeetNDiscuss
{    
    internal class ClientTCP
    {        
        private string _url;

        private EventWaitHandle onImage = new EventWaitHandle(false, EventResetMode.AutoReset);
        private int imageBufLocked;
        private byte[][] imageBuf = new byte[2][];
        private object[] imageBufLock = new object[] { new object(), new object() };
        private object rawLock = new object();
        private byte[] newImgData = null;
        private int[] newImgSZ = new int[2];
        private bool newImg = false;        

        public delegate void UpdateClientImage(BitmapSource image);
        public event UpdateClientImage OnUpdateClientImage;

        public MainWindow Window;        

        public ClientTCP(string url, MainWindow window)
        {
            _url = url;
            Window = window;
        }

        public void Start()
        {
            Task.Run(GetImage);
            Task.Run(GetRaw);
            Task.Run(Update);            
        }       

        private async Task Update()
        {            
            try
            {
                while (true)
                {
                    if (newImg)
                    {
                        newImg = false;                        

                        await Window.Dispatcher.InvokeAsync(() =>
                        {                            
                            BitmapSource newImage = BitmapSource.Create(newImgSZ[0], newImgSZ[1], 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, newImgData, newImgSZ[2]);                            
                            OnUpdateClientImage?.Invoke(newImage);
                        });                        
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private async Task GetImage()
        {
            HttpWebRequest req = null;
            WebResponse resp = null;
            Stream stream = null;

            try
            {                
                req = (HttpWebRequest)WebRequest.Create(_url);                
                resp = req.GetResponse();
                stream = resp.GetResponseStream();

                Debug.WriteLine("Ответ от сервера получен");
                
                while (true)
                {
                    int bytesToRead = FindLength(stream);                    
                    if (bytesToRead <= 0)                                            
                        break;                                        

                    int leftToRead = bytesToRead;
                    lock (imageBufLock[imageBufLocked])
                    {
                        imageBuf[imageBufLocked] = new byte[bytesToRead];
                        var buf = imageBuf[imageBufLocked];
                        while (leftToRead > 0)
                        {
                            var n = stream.Read(buf, bytesToRead - leftToRead, leftToRead);
                            if (n <= 0)
                                break;
                            leftToRead -= n;
                        }
                    }
                    if (leftToRead == 0)
                    {
                        imageBufLocked = imageBufLocked == 0 ? 1 : 0;
                        onImage.Set();                        
                    }

                    stream.ReadByte(); 
                    stream.ReadByte(); 
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }            

            if (req != null)
                req.Abort();

            if (stream != null)
            {
                stream.Close();
                stream.Dispose();
            }

            if (resp != null)
            {
                resp.Close();
                resp.Dispose();
            }

            await Window.Dispatcher.InvokeAsync(() =>
            {
                Window.StopAll();
            });
        }

        private async Task GetRaw()
        {
            var decompressor = new TJDecompressor();
            try
            {
                while (true)
                {
                    if (onImage.WaitOne(1000) == false)
                        continue;

                    var cb = imageBufLocked == 0 ? 1 : 0;
                    lock (imageBufLock[cb])
                    {
                        int w, h, s;
                        try
                        {
                            var raw = decompressor.Decompress(imageBuf[cb], TJPixelFormats.TJPF_RGBA, TJFlags.FASTUPSAMPLE, out w, out h, out s);

                            newImgData = new byte[raw.Length];
                            lock (rawLock)
                            {
                                newImgSZ = new int[] { w, h, s };
                                raw.CopyTo(newImgData, 0);                                
                                newImg = true;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                decompressor.Dispose();
            }
        }

        private int FindLength(Stream stream)
        {            
            int b;
            string line = "";
            int result = -1;
            bool atEOL = false;

            while ((b = stream.ReadByte()) != -1)
            {
                if (b == 10) continue;
                if (b == 13)
                {
                    if (atEOL)
                    {
                        stream.ReadByte();
                        return result;
                    }

                    if (line.StartsWith("Content-Length:"))
                        result = Convert.ToInt32(line.Substring("Content-Length:".Length).Trim());
                    else
                        line = "";

                    atEOL = true;
                }
                else
                {
                    atEOL = false;
                    line += (char)b;
                }
            }
            return -1;
        }
    }
}
