using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBScoreEmailResponse : ZBResponse
    {
        [JsonProperty("email")] public string Email;
        [JsonProperty("score")] public int Score;

        public override string ToString()
        {
            return "ZBScoreEmailResponse{" +
                   "email='" + Email + "'" +
                   ", score=" + Score +
                   '}';
        }
    }
}
