using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBS3ListFileEntry
    {
        public string Name { get; set; }

        public int Size { get; set; }

        [JsonProperty("last_modified")]
        public string LastModified { get; set; }
    }
}
