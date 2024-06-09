using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Windows;

namespace MeetNDiscuss
{
    class UdpAudioSender : IAudioSender
    {
        private readonly UdpClient udpSender;
        public UdpAudioSender(IPEndPoint endPoint)
        {            
            udpSender = new UdpClient();
            udpSender.Connect(endPoint);                            
        }

        public async void SendAsync(byte[] payload)
        {
            await udpSender.SendAsync(payload, payload.Length);
        }

        public void Dispose()
        {
            udpSender?.Close();
        }
    }
}