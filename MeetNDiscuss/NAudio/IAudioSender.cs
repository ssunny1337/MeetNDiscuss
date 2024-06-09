using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeetNDiscuss
{
    interface IAudioSender : IDisposable
    {
        void SendAsync(byte[] payload);
    }
}
