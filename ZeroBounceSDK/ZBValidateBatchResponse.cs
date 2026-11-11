using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBValidateBatchResponse : ZBResponse
    {
        [JsonProperty("email_batch")] public List<ZBValidateBatchResponseRow> EmailBatch { get; set; }

        [JsonProperty("errors")] public List<ZBBatchError> Errors { get; set; }

        public override string ToString()
        {
            return "ZBValidateBatchResponse{" +
                   "emailBatch=" + ListText(EmailBatch) +
                   ", errors=" + ListText(Errors) +
                   '}';
        }

        internal static string ListText<T>(List<T> items)
        {
            return items == null ? "null" : "[" + string.Join(", ", items) + "]";
        }
    }
}