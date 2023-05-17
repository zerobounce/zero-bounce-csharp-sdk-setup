using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBValidateEmailRow
    {
        [JsonProperty("email_address")] public string EmailAddress { get; set; }
        [JsonProperty("ip_address")] public string IpAddress { get; set; }
    }
}