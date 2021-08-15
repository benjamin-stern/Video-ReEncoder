using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReEncode.Data
{
    class VMAFData
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("VMAF score")]
        public float VMAFScore { get; set; }
    }
}
