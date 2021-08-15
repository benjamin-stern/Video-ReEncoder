using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReEncode.Utils
{
    class TextPairParser
    {
        Dictionary<string, string> Data = new Dictionary<string, string>();
        public TextPairParser(string data, string[] seperator = null) {
            seperator = seperator ?? new string[] { ":", "=" };

            string[] lines = data.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var parts = line.Split(seperator, StringSplitOptions.None).ToList();
                while (parts.Count > 1) {
                    var pair = parts.GetRange(0, 2);
                    var key = pair[0].ToLowerInvariant().Trim();
                    var value = pair[1].ToLowerInvariant().Trim();
                    Data[key] = value;
                    
                    parts.RemoveRange(0, 2);
                }
            }
        }

        public bool HasKey(string key) {
            return Data.ContainsKey(key);
        }

        public List<string> GetKeys() {
            return Data.Keys.ToList();
        }

        public List<string> GetKeysContains(string keyContains) {
            return Data.Keys.Where((key) => key.Contains(keyContains.ToLowerInvariant())).ToList();
            //var key = Data.Keys.Where((key) => key.Contains(keyContains.ToLowerInvariant())).ToList();
            //string result = "";

            //if (key != null)
            //{
            //    Data.TryGetValue(key, out result);
            //}

            //return result;
        }

        public string GetValue(string key) {
            string result = "";

            if (Data.ContainsKey(key)) {
                Data.TryGetValue(key, out result);
            }

            return result;
        }

        public void GetValue(string key, out int value)
        {
            int.TryParse(GetValue(key), out value);
        }

        public void GetValue(string key, out float value)
        {
            float.TryParse(GetValue(key), out value);
        }

        public void GetValue(string key, out long value)
        {
            long.TryParse(GetValue(key), out value);
        }

        public void GetValue(string key, out double value)
        {
            double.TryParse(GetValue(key), out value);
        }
    }
}
