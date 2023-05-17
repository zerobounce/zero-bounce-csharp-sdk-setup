using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBBatchError
    {
        [JsonProperty("error")] public string Error { get; set; }

        [JsonProperty("email_address")] public string EmailAddress { get; set; }
    }
}