using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBCreditsResponse : ZBResponse
    {
        [JsonProperty("Credits")] public string Credits;

        public override string ToString()
        {
            return "ZBCreditsResponse{" +
                   "credits='" + Credits + '\'' +
                   '}';
        }
    }
}