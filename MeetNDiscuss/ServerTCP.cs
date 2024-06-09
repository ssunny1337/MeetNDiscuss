using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using AForge.Video;
using System.Drawing;
using System.Windows.Threading;
using System.Windows;
using System.Net.Http;
using System.Windows.Media.Imaging;
using static MeetNDiscuss.ServerTCP;
using static MeetNDiscuss.ClientTCP;

namespace MeetNDiscuss
{
    internal class ServerTCP
    {        
        private TcpListener _listener;
        private bool _active = false;        

        private const string BOUNDARY = "MJPEG_Boundary";
        private const string CRLF = "\r\n";

        private string strHeader = "HTTP/1.0 200" + CRLF
                                 + "Cache-Control: no-cache" + CRLF
                                 + "Pragma: no-cache" + CRLF
                                 + "Connection: close" + CRLF
                                 + "Content-Type: multipart/x-mixed-replace; boundary=" + BOUNDARY
                                 + CRLF + CRLF;

        public EventWaitHandle jpgImageEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

        public byte[] CompressedImage;

        private object jpgImageLock = new object();

        public TcpClient CurrentClient { get; set; }

        public delegate void NewClient();
        public event NewClient OnNewClient;

        public MainWindow Window;

        public ServerTCP(int port, MainWindow window)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            Window = window;
        }

        public void Start()
        {
            _listener.Start();
            _active = true;

            Task.Run(AcceptClientAsync);          
        }

        private async Task AcceptClientAsync()
        {
            if (CurrentClient == null)
            {
                CurrentClient = await _listener.AcceptTcpClientAsync();
                OnNewClient?.Invoke();
                await Task.Run(async () => await ProcessClientAsync(CurrentClient));
                CurrentClient = null;
            }
        }        

        private async Task ProcessClientAsync(TcpClient tcpClient)
        {
            try
            {
                byte[] buf = new byte[1400];
                if (tcpClient.Client.Receive(buf) > 0)
                {
                    if (tcpClient.Client.Send(Encoding.ASCII.GetBytes(strHeader)) < strHeader.Length)
                    {
                        tcpClient.Close();
                        return;
                    }
                }
                else
                {
                    tcpClient.Close();
                    return;
                }

                while (tcpClient.Connected)
                {                        
                    jpgImageEvent.WaitOne();

                    byte[] img = null;
                    lock (jpgImageLock)
                    {
                        img = new byte[CompressedImage.Length];
                        CompressedImage.CopyTo(img, 0);
                    }

                    string h = "--" + BOUNDARY + CRLF;
                    h += "Content-Type: image/jpeg" + CRLF;
                    h += "Content-Length: " + img.Length.ToString() + CRLF + CRLF;

                    int n = 0;
                    if (tcpClient.Client != null)
                        n = tcpClient.Client.Send(ASCIIEncoding.ASCII.GetBytes(h));                        

                    if (n == h.Length)
                    {
                        await tcpClient.Client.SendAsync(img);

                        tcpClient.Client.Send(ASCIIEncoding.ASCII.GetBytes(CRLF+CRLF));
                    }
                    else
                        tcpClient.Close();
                }
                tcpClient.Close();
            }
            catch (SocketException ex)
            {                
                Debug.WriteLine("Клиент отключился");
                await Window.Dispatcher.InvokeAsync(() =>
                {
                    Window.VideoClientColumn.Width = new GridLength(0);
                });
                if (CurrentClient != null)
                    CurrentClient.Close();
                CurrentClient = null;
                Task.Run(AcceptClientAsync);
            }
        }

        public void Stop()
        {
            if (_active)
            {
                _active = false;
                _listener.Server.Close(0);
                _listener.Stop();

                if (CurrentClient != null)
                    CurrentClient.Close();
                CurrentClient = null;
            }
        }            
    }
}
