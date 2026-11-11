using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBActivityResponse : ZBResponse
    {
        [JsonProperty("found")] [JsonConverter(typeof(ZBStrictBoolConverter))] public bool Found;
        [JsonProperty("active_in_days")] public string ActiveInDays;

        public override string ToString()
        {
            return "ZBActivityResponse{" +
                   "found=" + Found +
                   ", activeInDays='" + ActiveInDays + "'" +
                   '}';
        }
    }
}