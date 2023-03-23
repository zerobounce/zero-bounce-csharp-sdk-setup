using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBCreditsResponse : ZBResponse
    {
        [JsonProperty("Credits")] public int Credits;

        public override string ToString()
        {
            return "ZBCreditsResponse{" +
                   "credits='" + Credits + '\'' +
                   '}';
        }
    }
}