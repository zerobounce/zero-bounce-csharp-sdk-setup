using System;
using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public enum ZBValidateSubStatus
    {
        None,
        AntispamSystem,
        Greylisted,
        MailServerTemporaryError,
        ForcibleDisconnect,
        MailServerDidNotRespond,
        TimeoutExceeded,
        FailedSmtpConnection,
        MailboxQuotaExceeded,
        ExceptionOccurred,
        PossibleTraps,
        RoleBased,
        GlobalSuppression,
        MailboxNotFound,
        NoDnsEntries,
        FailedSyntaxCheck,
        PossibleTypo,
        UnroutableIpAddress,
        LeadingPeriodRemoved,
        DoesNotAcceptMail,
        AliasAddress,
        RoleBasedCatchAll,
        Disposable,
        Toxic
    }
    
    public sealed class ZBValidateSubStatusConverter : JsonConverter {
        public override bool CanConvert(Type objectType) {
            return objectType == typeof(string);
        }

        public override object ReadJson(JsonReader reader, Type objectType,
            object existingValue, JsonSerializer serializer) {
            var value = (string)reader.Value;

            switch (value)
            {
                case "antispam_system":
                    return ZBValidateSubStatus.AntispamSystem;
                case "greylisted":
                    return ZBValidateSubStatus.Greylisted;
                case "mail_server_temporary_error":
                    return ZBValidateSubStatus.MailServerTemporaryError;
                case "forcible_disconnect":
                    return ZBValidateSubStatus.ForcibleDisconnect;
                case "mail_server_did_not_respond":
                    return ZBValidateSubStatus.MailServerDidNotRespond;
                case "timeout_exceeded":
                    return ZBValidateSubStatus.TimeoutExceeded;
                case "failed_smtp_connection":
                    return ZBValidateSubStatus.FailedSmtpConnection;
                case "mailbox_quota_exceeded":
                    return ZBValidateSubStatus.MailboxQuotaExceeded;
                case "exception_occurred":
                    return ZBValidateSubStatus.ExceptionOccurred;
                case "possible_traps":
                    return ZBValidateSubStatus.PossibleTraps;
                case "role_based":
                    return ZBValidateSubStatus.RoleBased;
                case "global_suppression":
                    return ZBValidateSubStatus.GlobalSuppression;
                case "mailbox_not_found":
                    return ZBValidateSubStatus.MailboxNotFound;
                case "no_dns_entries":
                    return ZBValidateSubStatus.NoDnsEntries;
                case "failed_syntax_check":
                    return ZBValidateSubStatus.FailedSyntaxCheck;
                case "possible_typo":
                    return ZBValidateSubStatus.PossibleTypo;
                case "unroutable_ip_address":
                    return ZBValidateSubStatus.UnroutableIpAddress;
                case "leading_period_removed":
                    return ZBValidateSubStatus.LeadingPeriodRemoved;
                case "does_not_accept_mail":
                    return ZBValidateSubStatus.DoesNotAcceptMail;
                case "alias_address":
                    return ZBValidateSubStatus.AliasAddress;
                case "role_based_catch_all":
                    return ZBValidateSubStatus.RoleBasedCatchAll;
                case "disposable":
                    return ZBValidateSubStatus.Disposable;
                case "toxic":
                    return ZBValidateSubStatus.Toxic;
                
                default:
                    return ZBValidateSubStatus.None;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            var status = (ZBValidateSubStatus) value;

            switch(status) {
                case ZBValidateSubStatus.AntispamSystem:
                    writer.WriteValue("antispam_system");
                    break;
                case ZBValidateSubStatus.Greylisted:
                    writer.WriteValue("greylisted");
                    break;
                case ZBValidateSubStatus.MailServerTemporaryError:
                    writer.WriteValue("mail_server_temporary_error");
                    break;
                case ZBValidateSubStatus.ForcibleDisconnect:
                    writer.WriteValue("forcible_disconnect");
                    break;
                case ZBValidateSubStatus.MailServerDidNotRespond:
                    writer.WriteValue("mail_server_did_not_respond");
                    break;
                case ZBValidateSubStatus.TimeoutExceeded:
                    writer.WriteValue("timeout_exceeded");
                    break;
                case ZBValidateSubStatus.FailedSmtpConnection:
                    writer.WriteValue("failed_smtp_connection");
                    break;
                case ZBValidateSubStatus.MailboxQuotaExceeded:
                    writer.WriteValue("mailbox_quota_exceeded");
                    break;
                case ZBValidateSubStatus.ExceptionOccurred:
                    writer.WriteValue("exception_occurred");
                    break;
                case ZBValidateSubStatus.PossibleTraps:
                    writer.WriteValue("possible_traps");
                    break;
                case ZBValidateSubStatus.RoleBased:
                    writer.WriteValue("role_based");
                    break;
                case ZBValidateSubStatus.GlobalSuppression:
                    writer.WriteValue("global_suppression");
                    break;
                case ZBValidateSubStatus.MailboxNotFound:
                    writer.WriteValue("mailbox_not_found");
                    break;
                case ZBValidateSubStatus.NoDnsEntries:
                    writer.WriteValue("no_dns_entries");
                    break;
                case ZBValidateSubStatus.FailedSyntaxCheck:
                    writer.WriteValue("failed_syntax_check");
                    break;
                case ZBValidateSubStatus.PossibleTypo:
                    writer.WriteValue("possible_typo");
                    break;
                case ZBValidateSubStatus.UnroutableIpAddress:
                    writer.WriteValue("unroutable_ip_address");
                    break;
                case ZBValidateSubStatus.LeadingPeriodRemoved:
                    writer.WriteValue("leading_period_removed");
                    break;
                case ZBValidateSubStatus.DoesNotAcceptMail:
                    writer.WriteValue("does_not_accept_mail");
                    break;
                case ZBValidateSubStatus.AliasAddress:
                    writer.WriteValue("alias_address");
                    break;
                case ZBValidateSubStatus.RoleBasedCatchAll:
                    writer.WriteValue("role_based_catch_all");
                    break;
                case ZBValidateSubStatus.Disposable:
                    writer.WriteValue("disposable");
                    break;
                case ZBValidateSubStatus.Toxic:
                    writer.WriteValue("toxic");
                    break;
               
                case ZBValidateSubStatus.None:
                default:
                    writer.WriteValue("none");
                    break;
            }
        }
    }
}