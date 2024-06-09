using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Xps.Packaging;
using AForge.Video;
using AForge.Video.DirectShow;
using MeetNDiscuss.NAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using SimpleTCP;
using TurboJpegWrapper;

namespace MeetNDiscuss
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool connected = false;

        private Config config = new Config();
        private string configPath = "config.json";

        private ServerTCP _server = null;
        private ClientTCP _client = null;

        private NetworkAudioPlayer player;
        private NetworkAudioSender audioSender;
        private UncompressedPcmChatCodec codec;

        public ObservableCollection<FilterInfo> VideoDevices { get; set; }

        public static readonly DependencyProperty CurrentDeviceProperty =
            DependencyProperty.Register("CurrentDevice",
                                        typeof(FilterInfo),
                                        typeof(MainWindow),
                                        new FrameworkPropertyMetadata(new FilterInfo(string.Empty), FrameworkPropertyMetadataOptions.AffectsRender));    
        
        public FilterInfo CurrentDevice
        {
            get { return (FilterInfo)GetValue(CurrentDeviceProperty); }
            set { SetValue(CurrentDeviceProperty, value); }
        }

        public static readonly DependencyProperty IsChatOpenProperty =
            DependencyProperty.Register("IsChatOpen",
                                        typeof(bool),
                                        typeof(MainWindow),
                                        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool IsChatOpen
        {
            get { return (bool)GetValue(IsChatOpenProperty); }
            set { SetValue(IsChatOpenProperty, value); }
        }

        private IVideoSource _videoSource;

        private object sync = new object();

        private object videoClientLock = new object();

        private bool _isMuted = false;
        private bool _isCameraOn = true;

        private BitmapSource _currentClientFrame;

        private SimpleTcpServer _chatServer;
        private SimpleTcpClient _chatClient;

        public ObservableCollection<MessageItem> MessageItems { get; set; }

        private bool _isStreamingScreen = false;

        public class Config
        {
            public int CompressionQuality;
            public int TargetFPS;            
            public int Port;            

            public Config(int compressionQuality, int targetFPS, int port)
            {
                CompressionQuality = compressionQuality;
                TargetFPS = targetFPS;
                Port = port;
            }

            public Config() { }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO
        {
            public Int32 cbSize;
            public Int32 flags;
            public IntPtr hCursor;
            public POINTAPI ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINTAPI
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        static extern bool DrawIcon(IntPtr hDC, int X, int Y, IntPtr hIcon);

        const Int32 CURSOR_SHOWING = 0x00000001;

        public MainWindow()
        {
            MessageItems = new ObservableCollection<MessageItem>();

            InitializeComponent();   
            DataContext = this;
            
            GetVideoDevices();
            GetAudioInputDevices();

            try { config = ConfigManager.Load<Config>(System.IO.Path.Combine(Environment.CurrentDirectory, configPath)); }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private void StartServer(int port)
        {                        
            _server = new ServerTCP(port, this);            
            _server.Start();
        }

        private void CloseServer()
        {
            if (_server != null)
                _server.Stop();
        }
                
        private void StartDiscussionButton_Click(object sender, RoutedEventArgs e)
        {
            RoomGrid.Visibility = Visibility.Visible;
            HomeGrid.Visibility = Visibility.Hidden;

            string localIp = GetLocalIPAddress();

            string ipSipher = EncryptionHelper.Encrypt(localIp);

            ConnectionCodeRun.Text = ipSipher;

            VideoClientColumn.Width = new GridLength(0);

            StartCamera();
            VideoHostCameraOff.Visibility = Visibility.Hidden;

            StartServer(5000);
            codec = new UncompressedPcmChatCodec();

            Task.Run(ConnectClient);            

            _chatServer = new SimpleTcpServer();                        

            _chatServer.DataReceived += (sender, e) =>
            {
                MessageItem newMessage = new MessageItem("Собеседник", Encoding.UTF8.GetString(e.Data));
                Dispatcher.Invoke(() =>
                {
                    MessageItems.Insert(0, newMessage);
                    ChatListBox.ScrollIntoView(newMessage);
                });
            };

            _chatServer.Start(5556);                        
        }        

        private void JoinDiscussionButton_Click(object sender, RoutedEventArgs e)
        {
            ConnectionCodeTextBox.Text = string.Empty;
            EnterConnectionCodeStackPanel.Visibility = Visibility.Visible;
            HomeGrid.Visibility = Visibility.Hidden;                        
        }

        private void ConnectToRoomButton_Click(object sender, RoutedEventArgs e)
        {            
            StartCamera();
            VideoHostCameraOff.Visibility = Visibility.Hidden;
            StartServer(9000);

            string ip = EncryptionHelper.Decrypt(ConnectionCodeTextBox.Text);

            _client = new ClientTCP($"http://{ip}:5000", this);
            _client.OnUpdateClientImage += SetClientImage;
            _client.Start();
            
            codec = new UncompressedPcmChatCodec();

            StartSendingAudio(new IPEndPoint(IPAddress.Parse(ip), 7080), WaveInDevicesComboBox.SelectedIndex, codec);
            StartReceivingAudio(9005, codec);

            _chatClient = new SimpleTcpClient();

            _chatClient.DataReceived += (sender, e) =>
            {
                MessageItem newMessage = new MessageItem("Собеседник", Encoding.UTF8.GetString(e.Data));
                Dispatcher.Invoke(() =>
                {
                    MessageItems.Insert(0, newMessage);
                    ChatListBox.ScrollIntoView(newMessage);
                });
            };

            _chatClient.Connect(ip, 5556);            

            connected = true;

            RoomGrid.Visibility = Visibility.Visible;
            EnterConnectionCodeStackPanel.Visibility = Visibility.Hidden;            
            ConnectionCodeTextBlock.Visibility = Visibility.Hidden;
            CopySipherCodeButton.Visibility = Visibility.Hidden;
        }

        private void SetClientImage(BitmapSource image)
        {
            if (image == null)
            {
                VideoClientCameraOff.Visibility = Visibility.Visible;
                return;
            }
            VideoClient.Source = image;            
            VideoClientCameraOff.Visibility = Visibility.Hidden;
        }

        private async void ConnectClient()
        {
            while (_server.CurrentClient == null)
                continue;            

            IPEndPoint remoteIPEndPoint = (IPEndPoint)_server.CurrentClient.Client.RemoteEndPoint;
            remoteIPEndPoint.Port = 9000;
            _client = new ClientTCP($"http://{remoteIPEndPoint.Address}:{remoteIPEndPoint.Port}", this);
            _client.OnUpdateClientImage += SetClientImage;            
            _client.Start();

            await Dispatcher.InvokeAsync(() =>
            {
                VideoClientColumn.Width = new GridLength(1, GridUnitType.Star);
                codec = new UncompressedPcmChatCodec();
                StartSendingAudio(new IPEndPoint(remoteIPEndPoint.Address, 9005), WaveInDevicesComboBox.SelectedIndex, codec);
                StartReceivingAudio(7080, codec);
            });

            connected = true;
        }

        #region Video Section

        public async void ProcessNewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            var compressor = new TJCompressor();
            try
            {
                Bitmap imageForUI = (Bitmap)eventArgs.Frame.Clone();
                imageForUI.RotateFlip(RotateFlipType.RotateNoneFlipX);
                await Dispatcher.InvokeAsync(() =>
                {
                    var bi = imageForUI.ToBitmapImage();
                    VideoHost.Source = bi;
                });

                imageForUI.RotateFlip(RotateFlipType.RotateNoneFlipX);

                if (_server == null)
                    return;

                var pixelFormat = imageForUI.PixelFormat;

                var width = imageForUI.Width;
                var height = imageForUI.Height;
                var srcData = imageForUI.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, pixelFormat);

                int size = srcData.Height * srcData.Stride;

                byte[] rawImage = new byte[size];

                Marshal.Copy(srcData.Scan0, rawImage, 0, size);

                byte[] buff = null;

                buff = compressor.Compress(rawImage, srcData.Stride, width, height, TJPixelFormats.TJPF_RGB, TJSubsamplingOptions.TJSAMP_420, config.CompressionQuality, TJFlags.FASTUPSAMPLE);

                if (buff != null && buff.Length > 0)
                {
                    _server.CompressedImage = new byte[buff.Length];
                    buff.CopyTo(_server.CompressedImage, 0);
                    _server.jpgImageEvent.Set();
                }
            }
            catch (Exception ex)
            {
                StopCamera();
                StartCamera();
            }
            finally
            {
                compressor.Dispose();
            }
        }

        public async void StreamScreen()
        {
            while (_isStreamingScreen)
            {
                var compressor = new TJCompressor();

                Bitmap screenshot = new Bitmap(1920, 1080);
                
                using (Graphics graphics = Graphics.FromImage(screenshot))
                {
                    graphics.CopyFromScreen(0, 0, 0, 0, screenshot.Size);
                        
                        CURSORINFO pci;
                        pci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));

                        if (GetCursorInfo(out pci))
                        {
                            if (pci.flags == CURSOR_SHOWING)
                            {
                                DrawIcon(graphics.GetHdc(), pci.ptScreenPos.x, pci.ptScreenPos.y, pci.hCursor);
                                graphics.ReleaseHdc();
                            }
                        }
                }

                Bitmap imageForUI = (Bitmap)screenshot.Clone();                
                await Dispatcher.InvokeAsync(() =>
                {
                    var bi = imageForUI.ToBitmapImage();
                    VideoHost.Source = bi;
                });

                if (_server == null)
                    return;

                var pixelFormat = imageForUI.PixelFormat;

                var width = imageForUI.Width;
                var height = imageForUI.Height;
                var srcData = imageForUI.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, pixelFormat);

                int size = srcData.Height * srcData.Stride;

                byte[] rawImage = new byte[size];

                Marshal.Copy(srcData.Scan0, rawImage, 0, size);

                byte[] buff = null;

                buff = compressor.Compress(rawImage, srcData.Stride, width, height, TJPixelFormats.TJPF_RGBA, TJSubsamplingOptions.TJSAMP_420, config.CompressionQuality, TJFlags.FASTUPSAMPLE);

                if (buff != null && buff.Length > 0)
                {
                    _server.CompressedImage = new byte[buff.Length];
                    buff.CopyTo(_server.CompressedImage, 0);
                    _server.jpgImageEvent.Set();
                }

                screenshot.Dispose();
                compressor.Dispose();
            }            
        }

        private void GetVideoDevices()
        {
            VideoDevices = new ObservableCollection<FilterInfo>();
            foreach (FilterInfo filterInfo in new FilterInfoCollection(FilterCategory.VideoInputDevice))
            {
                VideoDevices.Add(filterInfo);
            }
            if (VideoDevices.Any())
            {
                CurrentDevice = VideoDevices[0];
            }
            else
            {
                MessageBox.Show("No video sources found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartCamera()
        {
            if (CurrentDevice != null)
            {
                _videoSource = new VideoCaptureDevice(CurrentDevice.MonikerString);
                _videoSource.NewFrame += ProcessNewFrame;
                _videoSource.Start();                
            }
        }

        private void StopCamera()
        {
            if (_videoSource != null && _videoSource.IsRunning)
            {
                _videoSource.SignalToStop();
                _videoSource.NewFrame -= new NewFrameEventHandler(ProcessNewFrame);
            }
        }

        #endregion

        #region Audio Section

        private void GetAudioInputDevices()
        {
            for (int n = 0; n < WaveIn.DeviceCount; n++)
            {
                var capabilities = WaveIn.GetCapabilities(n);
                WaveInDevicesComboBox.Items.Add(capabilities.ProductName);
            }
            if (WaveInDevicesComboBox.Items.Count > 0)
            {
                WaveInDevicesComboBox.SelectedIndex = 0;
            }
        }

        private void StartSendingAudio(IPEndPoint endPoint, int inputDeviceNumber, INetworkChatCodec codec)
        {
            var sender = (IAudioSender)new UdpAudioSender(endPoint);
            
            audioSender = new NetworkAudioSender(codec, inputDeviceNumber, sender);             
        }

        private void StartReceivingAudio(int port, INetworkChatCodec codec)
        {
            var receiver = (IAudioReceiver)new UdpAudioReceiver(port);

            player = new NetworkAudioPlayer(codec, receiver);            
        }

        private void Disconnect()
        {
            if (connected)
            {
                player.Dispose();
                audioSender.Dispose();
                codec.Dispose();
                connected = false;
            }
        }

        #endregion

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsGrid.Visibility = Visibility.Visible;
            HomeGrid.Visibility = Visibility.Hidden;
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            HomeGrid.Visibility = Visibility.Visible;
            SettingsGrid.Visibility = Visibility.Hidden;
        }

        private void BackToHomeGridButton_Click(object sender, RoutedEventArgs e)
        {
            HomeGrid.Visibility = Visibility.Visible;
            RoomGrid.Visibility = Visibility.Hidden;
            StopAll();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            StopAll();
        }

        public void StopAll()
        {            
            ConnectionCodeTextBlock.Visibility = Visibility.Visible;
            CopySipherCodeButton.Visibility = Visibility.Visible;
            _server?.CurrentClient?.Close();
            _server?.Stop();
            _client = null;
            Disconnect();
            StopCamera();
            CloseServer();
            audioSender?.Dispose();
            player?.Dispose();
            _chatClient?.TcpClient?.Close();
            _chatServer?.Stop();
            _chatServer = null;
            _chatClient = null;
            VideoHostCameraOff.Visibility = Visibility.Visible;
            IsOffMonitorImage.Visibility = Visibility.Visible;

            HomeGrid.Visibility = Visibility.Visible;
            RoomGrid.Visibility = Visibility.Hidden;

            MessageItems.Clear();
            _isStreamingScreen = false;
        }

        public static string GetLocalIPAddress()
        {
            IPHostEntry ip = Dns.GetHostEntry(Dns.GetHostName());

            foreach (var address in ip.AddressList)
            {
                if (address.ToString().Contains("192.168"))
                {
                    return address.ToString();
                }
            }

            return string.Empty;
        }

        private void CopySipherCodeButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ConnectionCodeRun.Text);
            //Debug.WriteLine(ConnectionCodeRun.Text);
        }

        private void TurnOffMicroButton_Click(object sender, RoutedEventArgs e)
        {
            IsOffMicroImage.Visibility = IsOffMicroImage.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;

            using var enumerator = new MMDeviceEnumerator();
            var commDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            _isMuted = !_isMuted;
            commDevice.AudioEndpointVolume.Mute = _isMuted;                     
        }

        private void TurnOffCameraButton_Click(object sender, RoutedEventArgs e)
        {
            IsOffCameraImage.Visibility = IsOffCameraImage.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;            

            if (_isCameraOn)
            {
                VideoHostCameraOff.Visibility = Visibility.Visible;
                VideoHost.Visibility = Visibility.Hidden;
                StopCamera();                
            }
            else
            {
                StartCamera();
                VideoHost.Visibility = Visibility.Visible;
                VideoHostCameraOff.Visibility = Visibility.Hidden;
            }

            _isCameraOn = !_isCameraOn;
        }

        private void ChatButton_Click(object sender, RoutedEventArgs e)
        {
            IsChatOpen = !IsChatOpen;
        }

        private void StreamScreenButton_Click(object sender, RoutedEventArgs e)
        {
            IsOffMonitorImage.Visibility = IsOffMonitorImage.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
            _isStreamingScreen = !_isStreamingScreen;

            if (_isStreamingScreen)
            {                                
                StopCamera();
                Task.Run(StreamScreen);
            }
            else
                StartCamera();            
        }

        private void SendChatMessageButton_Click(object sender, RoutedEventArgs e)
        {
            MessageItem newMessage = new MessageItem("Вы", EnterChatMessageTextBox.Text);
            MessageItems.Insert(0, newMessage);

            // one of them is null depending on the side                      
            _chatServer?.Broadcast(Encoding.UTF8.GetBytes(EnterChatMessageTextBox.Text));       
                                   
            _chatClient?.Write(Encoding.UTF8.GetBytes(EnterChatMessageTextBox.Text));                                            
            
            EnterChatMessageTextBox.Text = string.Empty;
            ChatListBox.ScrollIntoView(newMessage);
        }        
    }
}
