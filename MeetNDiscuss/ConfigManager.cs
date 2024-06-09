using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeetNDiscuss
{
    internal static class ConfigManager
    {
        public static T Load<T>(string filePath)
        {
            string data = File.ReadAllText(filePath);
            T config = JsonConvert.DeserializeObject<T>(data);
            return config;
        }

        public static void Save<T>(T obj, string filePath)
        {
            string data = JsonConvert.SerializeObject(obj);
            File.WriteAllText(filePath, data);
        }
    }
}
