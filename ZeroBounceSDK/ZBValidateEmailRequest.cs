using Newtonsoft.Json;
using System.Collections.Generic;

namespace ZeroBounceSDK
{
    public class ZBValidateEmailRequest : ZBJsonRequest
    {
        [JsonProperty("api_key")] public string ApiKey { get; set; }

        [JsonProperty("email_batch")] public List<ZBValidateEmailRow> EmailBatch { get; set; }

        /// <summary>
        /// LIVE: validatebatch takes timeout as a JSON <em>string</em>. Sent as
        /// a number the API rejects the whole batch with the account-level
        /// "Invalid API Key or your account ran out of credits".
        /// </summary>
        [JsonProperty("timeout", NullValueHandling = NullValueHandling.Ignore)]
        public string Timeout { get; set; }

        [JsonProperty("activity_data", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ActivityData { get; set; }

        [JsonProperty("verify_plus", NullValueHandling = NullValueHandling.Ignore)]
        public bool? VerifyPlus { get; set; }
    }
}