using System;
using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public class ZBValidateResponse : ZBResponse
    {
        //The email address you are validating.
        [JsonProperty("address")] public string Address;

        //[valid, invalid, catch-all, unknown, spamtrap, abuse, do_not_mail]
        [JsonProperty("status")] [JsonConverter(typeof(ZBValidateStatusConverter))]
        public ZBValidateStatus Status;

        //[antispam_system, greylisted, mail_server_temporary_error, forcible_disconnect, mail_server_did_not_respond, timeout_exceeded, failed_smtp_connection, mailbox_quota_exceeded, exception_occurred, possible_traps, role_based, global_suppression, mailbox_not_found, no_dns_entries, failed_syntax_check, possible_typo, unroutable_ip_address, leading_period_removed, does_not_accept_mail, alias_address, role_based_catch_all, disposable, toxic]
        [JsonProperty("sub_status")] [JsonConverter(typeof(ZBValidateSubStatusConverter))]
        public ZBValidateSubStatus SubStatus;

        //The portion of the email address before the "@" symbol.
        [JsonProperty("account")] public string Account;

        //The portion of the email address after the "@" symbol.
        [JsonProperty("domain")] public string Domain;

        //Suggestive Fix for an email typo
        [JsonProperty("did_you_mean")] public string DidYouMean;

        //Age of the email domain in days or [null].
        [JsonProperty("domain_age_days")] public int DomainAgeDays;

        //[true/false] If the email comes from a free provider.
        [JsonProperty("free_email")] public bool FreeEmail;

        //[true/false] Does the domain have an MX record.
        [JsonProperty("mx_found")] public bool MxFound;

        //The preferred MX record of the domain
        [JsonProperty("mx_record")] public string MxRecord;

        //The SMTP Provider of the email or [null] [BETA].
        [JsonProperty("smtp_provider")] public string SmtpProvider;

        //The first name of the owner of the email when available or [null].
        [JsonProperty("firstname")] public string FirstName;

        //The last name of the owner of the email when available or [null].
        [JsonProperty("lastname")] public string LastName;

        //The gender of the owner of the email when available or [null].
        [JsonProperty("gender")] public string Gender;

        //The city of the IP passed in.
        [JsonProperty("city")] public string City;

        //The region/state of the IP passed in.
        [JsonProperty("region")] public string Region;

        //The zipcode of the IP passed in.
        [JsonProperty("zipcode")] public string ZipCode;

        //The country of the IP passed in.
        [JsonProperty("country")] public string Country;

        //The UTC time the email was validated.
        [JsonProperty("processed_at")] public DateTime ProcessedAt;

        [JsonProperty("error")] public string Error;

        public override string ToString()
        {
            return "ZBValidateResponse{" +
                   "address='" + Address + '\'' +
                   ", account='" + Account + '\'' +
                   ", status=" + Status +
                   ", subStatus=" + SubStatus +
                   ", domain='" + Domain + '\'' +
                   ", didYouMean='" + DidYouMean + '\'' +
                   ", domainAgeDays='" + DomainAgeDays + '\'' +
                   ", freeEmail=" + FreeEmail +
                   ", mxFound=" + MxFound +
                   ", mxRecord='" + MxRecord + '\'' +
                   ", smtpProvider='" + SmtpProvider + '\'' +
                   ", firstName='" + FirstName + '\'' +
                   ", lastName='" + LastName + '\'' +
                   ", gender='" + Gender + '\'' +
                   ", city='" + City + '\'' +
                   ", region='" + Region + '\'' +
                   ", zipCode='" + ZipCode + '\'' +
                   ", country='" + Country + '\'' +
                   ", processedAt='" + ProcessedAt + '\'' +
                   ", error='" + Error + '\'' +
                   '}';
        }
    }
}