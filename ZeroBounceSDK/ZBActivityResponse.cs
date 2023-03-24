using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBActivityResponse : ZBResponse
    {
        [JsonProperty("found")] public bool Found;
        [JsonProperty("active_in_days")] public int ActiveInDays;

        public override string ToString()
        {
            return "ZBActivityResponse{" +
                   "found=" + Found +
                   ", activeInDays='" + ActiveInDays + "'" +
                   '}';
        }
    }
}