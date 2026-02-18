using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBEmailFinderResponse : ZBResponse
    {
        //The resulting email address identified by the API.
        [JsonProperty("email")] public string Email;

        //The level of confidence we have that the email exists. Possible values: high, medium, low, unknown, undetermined.
        [JsonProperty("email_confidence")] [JsonConverter(typeof(ZBConfidenceConverter))]
        public ZBConfidence EmailConfidence { get; set; }

        //The provided domain name.
        [JsonProperty("domain")] public string Domain;

        //The company associated with the domain.
        [JsonProperty("company_name")] public string CompanyName;

        //e.g. a suggestion in case case a firstname is used in the lastname field
        [JsonProperty("did_you_mean")] public string DidYouMean;

        //A reason for the unknown result. Possible values can be found in the "Possible reasons for unknown status" section below.
        [JsonProperty("failure_reason")] public string FailureReason;
    }
}