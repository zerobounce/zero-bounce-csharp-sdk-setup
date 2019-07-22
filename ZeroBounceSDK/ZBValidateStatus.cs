using System;
using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    public enum ZBValidateStatus
    {
        None,
        Valid,
        Invalid,
        CatchAll,
        Unknown,
        Spamtrap,
        Abuse,
        DoNotMail
    }
    
    
    public sealed class ZBValidateStatusConverter : JsonConverter {
        public override bool CanConvert(Type objectType) {
            return objectType == typeof(string);
        }

        public override object ReadJson(JsonReader reader, Type objectType,
            object existingValue, JsonSerializer serializer) {
            var value = (string)reader.Value;

            switch (value)
            {
                case "valid":
                    return ZBValidateStatus.Valid;
                case "invalid":
                    return ZBValidateStatus.Invalid;
                case "catch-all":
                    return ZBValidateStatus.CatchAll;
                case "unknown":
                    return ZBValidateStatus.Unknown;
                case "spamtrap":
                    return ZBValidateStatus.Spamtrap;
                case "abuse":
                    return ZBValidateStatus.Abuse;
                case "do_not_mail":
                    return ZBValidateStatus.DoNotMail;
                
                default:
                    return ZBValidateStatus.None;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            var status = (ZBValidateStatus) value;

            switch(status) {
                case ZBValidateStatus.Valid:
                    writer.WriteValue("valid");
                    break;
                case ZBValidateStatus.Invalid:
                    writer.WriteValue("invalid");
                    break;
                case ZBValidateStatus.CatchAll:
                    writer.WriteValue("catch-all");
                    break;
                case ZBValidateStatus.Unknown:
                    writer.WriteValue("unknown");
                    break;
                case ZBValidateStatus.Spamtrap:
                    writer.WriteValue("spamtrap");
                    break;
                case ZBValidateStatus.Abuse:
                    writer.WriteValue("abuse");
                    break;
                case ZBValidateStatus.DoNotMail:
                    writer.WriteValue("do_not_mail");
                    break;
                case ZBValidateStatus.None:
                default:
                    writer.WriteValue("none");
                    break;
            }
        }
    }
}