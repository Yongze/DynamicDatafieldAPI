using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicDatafieldAPI { 

    public partial class JSONObject
    {
        [JsonProperty("id")]
        public String ID { get; set; }

        [JsonProperty("name")]
        public String Name { get; set; }

        [JsonProperty("front")]
        public String Front { get; set; }

        [JsonProperty("back")]
        public String Back { get; set; }
    }

    
}
