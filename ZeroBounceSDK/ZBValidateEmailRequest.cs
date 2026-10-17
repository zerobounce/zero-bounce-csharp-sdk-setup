using Newtonsoft.Json;
using System.Collections.Generic;

namespace ZeroBounceSDK
{
    public class ZBValidateEmailRequest : ZBJsonRequest
    {
        [JsonProperty("api_key")] public string ApiKey { get; set; }

        [JsonProperty("email_batch")] public List<ZBValidateEmailRow> EmailBatch { get; set; }

        [JsonProperty("timeout", NullValueHandling = NullValueHandling.Ignore)]
        public int? Timeout { get; set; }

        [JsonProperty("activity_data", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ActivityData { get; set; }

        [JsonProperty("verify_plus", NullValueHandling = NullValueHandling.Ignore)]
        public bool? VerifyPlus { get; set; }
    }
}