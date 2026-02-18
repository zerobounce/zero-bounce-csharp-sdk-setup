using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBDomainSearchResponse : ZBResponse
    {
        //The provided domain name.
        [JsonProperty("domain")] public string Domain;

        //The company associated with the domain.
        [JsonProperty("company_name")] public string CompanyName;

        //The format of the resulting email address.
        [JsonProperty("format")] public string Format;

        //The level of confidence we have in the provided format. Possible values: high, medium, low, unknown, undetermined.
        [JsonProperty("confidence")] [JsonConverter(typeof(ZBConfidenceConverter))]
        public ZBConfidence Confidence { get; set; }

        //e.g. a suggestion in case case a firstname is used in the lastname field
        [JsonProperty("did_you_mean")] public string DidYouMean;

        //A reason for the unknown result. Possible values can be found in the "Possible reasons for unknown status" section below.
        [JsonProperty("failure_reason")] public string FailureReason;

        //A list of domain formats we have for the specified domain. This list contains other formats (e.g. first_name, first, first.last, etc.) and the level of confidence we have in the respective format.
        [JsonProperty("other_domain_formats")] public List<ZBDomainFormat> OtherDomainFormats { get; set; }
    }
}