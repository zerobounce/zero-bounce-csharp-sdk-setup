using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBCreditsResponse : ZBResponse
    {
        // A JSON string holding a whole number ("-1", "12345"); anything else,
        // or no Credits at all, is a failure with status 200 (E7).
        [JsonProperty("Credits", Required = Required.Always)] [JsonConverter(typeof(ZBStrictTypes.CreditsConverter))]
        public int Credits;

        public override string ToString()
        {
            return "ZBCreditsResponse{" +
                   "credits='" + Credits + '\'' +
                   '}';
        }
    }
}