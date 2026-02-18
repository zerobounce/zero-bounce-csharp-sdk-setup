using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

class CustomDateTimeConverter : IsoDateTimeConverter
{
    public CustomDateTimeConverter()
    {
        DateTimeFormat = "M/d/yyyy";
    }
}

namespace ZeroBounceSDK
{
    public class ZBGetApiUsageResponse : ZBResponse
    {
        [JsonProperty("total")] public int Total;

        /// Total valid email addresses returned by the API
        [JsonProperty("status_valid")] public int StatusValid;

        /// Total invalid email addresses returned by the API
        [JsonProperty("status_invalid")] public int StatusInvalid;

        /// Total catch-all email addresses returned by the API
        [JsonProperty("status_catch_all")] public int StatusCatchAll;

        /// Total do not mail email addresses returned by the API
        [JsonProperty("status_do_not_mail")] public int StatusDoNotMail;

        /// Total spamtrap email addresses returned by the API
        [JsonProperty("status_spamtrap")] public int StatusSpamtrap;

        /// Total unknown email addresses returned by the API
        [JsonProperty("status_unknown")] public int StatusUnknown;

        /// Total number of times the API has a sub status of "toxic"
        [JsonProperty("sub_status_toxic")] public int SubStatusToxic;

        /// Total number of times the API has a sub status of "disposable"
        [JsonProperty("sub_status_disposable")]
        public int SubStatusDisposable;

        /// Total number of times the API has a sub status of "role_based"
        [JsonProperty("sub_status_role_based")]
        public int SubStatusRoleBased;

        /// Total number of times the API has a sub status of "possible_trap"
        [JsonProperty("sub_status_possible_trap")]
        public int SubStatusPossibleTrap;

        /// Total number of times the API has a sub status of "global_suppression"
        [JsonProperty("sub_status_global_suppression")]
        public int SubStatusGlobalSuppression;

        /// Total number of times the API has a sub status of "timeout_exceeded"
        [JsonProperty("sub_status_timeout_exceeded")]
        public int SubStatusTimeoutExceeded;

        /// Total number of times the API has a sub status of "mail_server_temporary_error"
        [JsonProperty("sub_status_mail_server_temporary_error")]
        public int SubStatusMailServerTemporaryError;

        /// Total number of times the API has a sub status of "mail_server_did_not_respond"
        [JsonProperty("sub_status_mail_server_did_not_respond")]
        public int SubStatusMailServerDidNotResponse;

        /// Total number of times the API has a sub status of "greylisted"
        [JsonProperty("sub_status_greylisted")]
        public int SubStatusGreyListed;

        /// Total number of times the API has a sub status of "antispam_system"
        [JsonProperty("sub_status_antispam_system")]
        public int SubStatusAntiSpamSystem;

        /// Total number of times the API has a sub status of "does_not_accept_mail"
        [JsonProperty("sub_status_does_not_accept_mail")]
        public int SubStatusDoesNotAcceptMail;

        /// Total number of times the API has a sub status of "exception_occurred"
        [JsonProperty("sub_status_exception_occurred")]
        public int SubStatusExceptionOccurred;

        /// Total number of times the API has a sub status of "failed_syntax_check"
        [JsonProperty("sub_status_failed_syntax_check")]
        public int SubStatusFailedSyntaxCheck;

        /// Total number of times the API has a sub status of "mailbox_not_found"
        [JsonProperty("sub_status_mailbox_not_found")]
        public int SubStatusMailboxNotFound;

        /// Total number of times the API has a sub status of "unroutable_ip_address"
        [JsonProperty("sub_status_unroutable_ip_address")]
        public int SubStatusUnRoutableIpAddress;

        /// Total number of times the API has a sub status of "possible_typo"
        [JsonProperty("sub_status_possible_typo")]
        public int SubStatusPossibleTypo;

        /// Total number of times the API has a sub status of "no_dns_entries"
        [JsonProperty("sub_status_no_dns_entries")]
        public int SubStatusNoDnsEntries;

        /// Total role based catch alls the API has a sub status of "role_based_catch_all"
        [JsonProperty("sub_status_role_based_catch_all")]
        public int SubStatusRoleBasedCatchAll;

        /// Total number of times the API has a sub status of "mailbox_quota_exceeded"
        [JsonProperty("sub_status_mailbox_quota_exceeded")]
        public int SubStatusMailboxQuotaExceeded;

        /// Total forcible disconnects the API has a sub status of "forcible_disconnect"
        [JsonProperty("sub_status_forcible_disconnect")]
        public int SubStatusForcibleDisconnect;

        /// Total failed SMTP connections the API has a sub status of "failed_smtp_connection"
        [JsonProperty("sub_status_failed_smtp_connection")]
        public int SubStatusFailedSmtpConnection;

        /// Total number of times the API has a sub status of "accept_all"
        [JsonProperty("sub_status_accept_all")]
        public int SubStatusAcceptAll;

        /// Total number of times the API has a sub status of "mx_forward"
        [JsonProperty("sub_status_mx_forward")]
        public int SubStatusMxForward;

        /// Total number of times the API has a sub status of "alternate"
        [JsonProperty("sub_status_alternate")]
        public int SubStatusAlternate;

        /// Total number of times the API has a sub status of "allowed"
        [JsonProperty("sub_status_allowed")]
        public int SubStatusAllowed;

        /// Total number of times the API has a sub status of "blocked"
        [JsonProperty("sub_status_blocked")]
        public int SubStatusBlocked;

        /// Total number of times the API has a sub status of "gold"
        [JsonProperty("sub_status_gold")]
        public int SubStatusGold;

        /// Start date of query
        [JsonConverter(typeof(CustomDateTimeConverter))]
        [JsonProperty("start_date")] public DateTime StartDate;

        /// End date of query
        [JsonConverter(typeof(CustomDateTimeConverter))]
        [JsonProperty("end_date")] public DateTime EndDate;

        public override string ToString()
        {
            return "ZBGetApiUsageResponse{" +
                   "total=" + Total +
                   ", statusValid=" + StatusValid +
                   ", statusInvalid=" + StatusInvalid +
                   ", statusCatchAll=" + StatusCatchAll +
                   ", statusDoNotMail=" + StatusDoNotMail +
                   ", statusSpamtrap=" + StatusSpamtrap +
                   ", statusUnknown=" + StatusUnknown +
                   ", subStatusToxic=" + SubStatusToxic +
                   ", subStatusDisposable=" + SubStatusDisposable +
                   ", subStatusRoleBased=" + SubStatusRoleBased +
                   ", subStatusPossibleTrap=" + SubStatusPossibleTrap +
                   ", subStatusGlobalSuppression=" + SubStatusGlobalSuppression +
                   ", subStatusTimeoutExceeded=" + SubStatusTimeoutExceeded +
                   ", subStatusMailServerTemporaryError=" + SubStatusMailServerTemporaryError +
                   ", subStatusMailServerDidNotResponse=" + SubStatusMailServerDidNotResponse +
                   ", subStatusGreyListed=" + SubStatusGreyListed +
                   ", subStatusAntiSpamSystem=" + SubStatusAntiSpamSystem +
                   ", subStatusDoesNotAcceptMail=" + SubStatusDoesNotAcceptMail +
                   ", subStatusExceptionOccurred=" + SubStatusExceptionOccurred +
                   ", subStatusFailedSyntaxCheck=" + SubStatusFailedSyntaxCheck +
                   ", subStatusMailboxNotFound=" + SubStatusMailboxNotFound +
                   ", subStatusUnRoutableIpAddress=" + SubStatusUnRoutableIpAddress +
                   ", subStatusPossibleTypo=" + SubStatusPossibleTypo +
                   ", subStatusNoDnsEntries=" + SubStatusNoDnsEntries +
                   ", subStatusRoleBasedCatchAll=" + SubStatusRoleBasedCatchAll +
                   ", subStatusMailboxQuotaExceeded=" + SubStatusMailboxQuotaExceeded +
                   ", subStatusForcibleDisconnect=" + SubStatusForcibleDisconnect +
                   ", subStatusFailedSmtpConnection=" + SubStatusFailedSmtpConnection +
                   ", subStatusAcceptAll=" + SubStatusAcceptAll +
                   ", subStatusMxForward=" + SubStatusMxForward +
                   ", subStatusAlternate=" + SubStatusAlternate +
                   ", subStatusAllowed=" + SubStatusAllowed +
                   ", subStatusBlocked=" + SubStatusBlocked +
                   ", subStatusGold=" + SubStatusGold +
                   ", startDate='" + StartDate + '\'' +
                   ", endDate='" + EndDate + '\'' +
                   '}';
        }
    }
}