using System;
using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    /// <summary>
    /// Confidence level for Email Finder / Domain Search (guess format) results.
    /// Matches Python SDK ZBConfidence.
    /// </summary>
    public enum ZBConfidence
    {
        Unknown,
        High,
        Medium,
        Low,
        Undetermined
    }

    /// <summary>
    /// Converts API string values (e.g. "high", "medium") to ZBConfidence.
    /// </summary>
    public sealed class ZBConfidenceConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ZBConfidence);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null || reader.Value == null)
                return ZBConfidence.Unknown;

            var value = (reader.Value as string)?.ToLowerInvariant() ?? "";
            switch (value)
            {
                case "high": return ZBConfidence.High;
                case "medium": return ZBConfidence.Medium;
                case "low": return ZBConfidence.Low;
                case "undetermined": return ZBConfidence.Undetermined;
                default: return ZBConfidence.Unknown;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var c = (ZBConfidence)value;
            switch (c)
            {
                case ZBConfidence.High: writer.WriteValue("high"); break;
                case ZBConfidence.Medium: writer.WriteValue("medium"); break;
                case ZBConfidence.Low: writer.WriteValue("low"); break;
                case ZBConfidence.Undetermined: writer.WriteValue("undetermined"); break;
                default: writer.WriteValue("unknown"); break;
            }
        }
    }
}
