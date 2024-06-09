using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MeetNDiscuss
{

    public class MessageItem : DependencyObject
    {
        public string Name { get; set; }
        public string Message { get; set; }     

        public MessageItem(string name, string message)
        {
            Name = name;
            Message = message;
        }
    }
}
