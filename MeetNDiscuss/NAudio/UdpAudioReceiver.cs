using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MeetNDiscuss
{
    class UdpAudioReceiver : IAudioReceiver
    {
        private Action<byte[]> handler;
        private readonly UdpClient udpListener;
        private bool listening;

        public UdpAudioReceiver(int portNumber)
        {
            //var endPoint = new IPEndPoint(IPAddress.Loopback, portNumber);

            udpListener = new UdpClient(portNumber);

            // To allow us to talk to ourselves for test purposes:
            // http://stackoverflow.com/questions/687868/sending-and-receiving-udp-packets-between-two-programs-on-the-same-computer
            udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            //udpListener.Client.Bind(endPoint);

            ThreadPool.QueueUserWorkItem(ListenerThread);
            listening = true;
        }

        private async void ListenerThread(object state)
        {
            //var endPoint = (IPEndPoint)state;
            try
            {
                while (listening)
                {
                    var result = await udpListener.ReceiveAsync();
                    handler?.Invoke(result.Buffer);
                }
            }
            catch (SocketException)
            {
                // usually not a problem - just means we have disconnected
            }
        }

        public void Dispose()
        {
            listening = false;
            udpListener?.Close();
        }

        public void OnReceived(Action<byte[]> onAudioReceivedAction)
        {
            handler = onAudioReceivedAction;
        }
    }
}