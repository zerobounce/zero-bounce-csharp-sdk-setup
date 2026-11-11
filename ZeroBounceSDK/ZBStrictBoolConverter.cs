using System;
using Newtonsoft.Json;

namespace ZeroBounceSDK
{
    /// <summary>
    /// A response value the SDK refuses to guess at. Its message is the short
    /// text the failure callback receives, and never contains the body.
    /// </summary>
    internal sealed class ZBInvalidValueException : JsonSerializationException
    {
        public ZBInvalidValueException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Reads a boolean strictly: <c>true</c>/<c>false</c>, the strings
    /// <c>"true"</c>/<c>"false"</c> in any case, and <c>1</c>/<c>0</c> or
    /// <c>"1"</c>/<c>"0"</c>. Anything else is an error, rather than
    /// Newtonsoft's default of reading every non-zero number as true and
    /// failing on "1". A JSON null is <c>null</c> for <c>bool?</c> and
    /// <c>false</c> for <c>bool</c>.
    /// </summary>
    internal sealed class ZBStrictBoolConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(bool) || objectType == typeof(bool?);
        }

        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Null:
                case JsonToken.Undefined:
                    return objectType == typeof(bool?) ? (object)null : false;
                case JsonToken.Boolean:
                    return (bool)reader.Value;
                case JsonToken.Integer:
                    if (reader.Value is long number)
                    {
                        if (number == 1) return true;
                        if (number == 0) return false;
                    }
                    break;
                case JsonToken.String:
                    var text = (string)reader.Value;
                    if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || text == "1") return true;
                    if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) || text == "0") return false;
                    break;
            }
            throw new ZBInvalidValueException("ZeroBounce returned an invalid boolean for " + FieldName(reader.Path));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        /// <summary>The JSON field name, without the array position of a batch row.</summary>
        private static string FieldName(string path)
        {
            var dot = (path ?? "").LastIndexOf('.');
            return dot < 0 ? path : path.Substring(dot + 1);
        }
    }
}
