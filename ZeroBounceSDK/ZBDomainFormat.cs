using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBDomainFormat
    {
        [JsonProperty("format")] public string Format { get; set; }

        [JsonProperty("confidence")] [JsonConverter(typeof(ZBConfidenceConverter))]
        public ZBConfidence Confidence { get; set; }

        public override string ToString()
        {
            return "ZBDomainFormat{" +
                   "format='" + Format + '\'' +
                   ", confidence=" + Confidence +
                   '}';
        }
    }
}