using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBDomainFormat
    {
        [JsonProperty("format")] public string Format { get; set; }
        [JsonProperty("confidence")] public string Confidence { get; set; }
    }
}