using Newtonsoft.Json;
using System.Collections.Generic;

namespace ZeroBounceSDK
{
    public class ZBValidateEmailRequest : ZBJsonRequest
    {
        [JsonProperty("api_key")] public string ApiKey { get; set; }

        [JsonProperty("email_batch")] public List<ZBValidateEmailRow> EmailBatch { get; set; }
    }
}