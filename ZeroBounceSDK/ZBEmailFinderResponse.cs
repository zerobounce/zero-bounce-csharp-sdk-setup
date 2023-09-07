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

        //The provided domain name.
        [JsonProperty("domain")] public string Domain;

        //The format of the resulting email address.
        [JsonProperty("format")] public string Format;

        //[valid, invalid, catch-all, unknown, spamtrap, abuse, do_not_mail]
        [JsonProperty("status")]
        [JsonConverter(typeof(ZBValidateStatusConverter))]
        public ZBValidateStatus Status;

        //[antispam_system, greylisted, mail_server_temporary_error, forcible_disconnect, mail_server_did_not_respond, timeout_exceeded, failed_smtp_connection, mailbox_quota_exceeded, exception_occurred, possible_traps, role_based, global_suppression, mailbox_not_found, no_dns_entries, failed_syntax_check, possible_typo, unroutable_ip_address, leading_period_removed, does_not_accept_mail, alias_address, role_based_catch_all, disposable, toxic]
        [JsonProperty("sub_status")]
        [JsonConverter(typeof(ZBValidateSubStatusConverter))]
        public ZBValidateSubStatus SubStatus;

        //The level of confidence we have in the provided format based on our engine and database. Possible values: LOW, MEDIUM, HIGH, UNKNOWN.
        [JsonProperty("confidence")] public string Confidence;

        //e.g. a suggestion in case case a firstname is used in the lastname field
        [JsonProperty("did_you_mean")] public string DidYouMean;

        //A reason for the unknown result. Possible values can be found in the "Possible reasons for unknown status" section below.
        [JsonProperty("failure_reason")] public string FailureReason;

        //A list of domain formats we have for the specified domain. This list contains other formats (e.g. first_name, first, first.last, etc.) and the level of confidence we have in the respective format.
        [JsonProperty("other_domain_formats")] public List<ZBDomainFormat> OtherDomainFormats { get; set; }
    }
}