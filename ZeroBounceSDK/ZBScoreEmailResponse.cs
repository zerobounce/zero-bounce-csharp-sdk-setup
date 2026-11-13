using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBScoreEmailResponse : ZBResponse
    {
        [JsonProperty("email")] public string Email;
        // The AI score is a whole number, sent as a JSON integer or a string holding one (E7).
        [JsonProperty("score")] [JsonConverter(typeof(ZBStrictTypes.IntegerConverter))] public int Score;

        public override string ToString()
        {
            return "ZBScoreEmailResponse{" +
                   "email='" + Email + "'" +
                   ", score=" + Score +
                   '}';
        }
    }
}
