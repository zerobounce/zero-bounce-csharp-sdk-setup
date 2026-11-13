using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ZeroBounceSDK
{
    /// <summary>
    /// Field types are part of the shape (check E7): a typed result's field that
    /// arrives with another JSON type is a failure with status 200, never coerced
    /// (<c>true</c> read as <c>"True"</c>), defaulted, or a cast exception with
    /// status 0. <c>null</c> is accepted for an optional field.
    /// </summary>
    internal static class ZBStrictTypes
    {
        /// <summary>
        /// The settings every typed result is read with. Date-looking strings stay
        /// strings until a <see cref="DateTime"/> member reads them, as before, so a
        /// string member gets the text the API sent.
        /// </summary>
        internal static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None,
            Converters = { new StrictStringConverter() },
        };

        private static readonly Regex Integer = new Regex("^-?[0-9]+$");

        internal static string Kind(JsonToken token)
        {
            switch (token)
            {
                case JsonToken.Boolean: return "a boolean";
                case JsonToken.Integer:
                case JsonToken.Float: return "a number";
                case JsonToken.StartArray: return "an array";
                case JsonToken.StartObject: return "an object";
                case JsonToken.Null:
                case JsonToken.Undefined: return "null";
                default: return "a string";
            }
        }

        private static string Kind(JToken token)
        {
            if (token == null) return "no value";
            switch (token.Type)
            {
                case JTokenType.Null: return "null";
                case JTokenType.Boolean: return "a boolean";
                case JTokenType.Integer:
                case JTokenType.Float: return "a number";
                case JTokenType.Array: return "an array";
                case JTokenType.Object: return "an object";
                default: return "a string";
            }
        }

        /// <summary>The JSON field name, without the array position of a batch row.</summary>
        internal static string FieldName(string path)
        {
            var dot = (path ?? "").LastIndexOf('.');
            return dot < 0 ? path : path.Substring(dot + 1);
        }

        /// <summary>Throws for a value that is neither a JSON string nor null; otherwise returns the string.</summary>
        internal static string ReadString(JsonReader reader, string expected = "a string")
        {
            if (reader.TokenType == JsonToken.Null || reader.TokenType == JsonToken.Undefined)
                return null;
            if (reader.TokenType == JsonToken.String)
                return (string)reader.Value;
            var found = Kind(reader.TokenType);
            var path = reader.Path;
            reader.Skip();
            throw new ZBInvalidValueException("ZeroBounce returned " + found + " for " + FieldName(path)
                                              + ", which is not " + expected + ".");
        }

        /// <summary>
        /// Validate's <c>address</c> and <c>status</c> must be strings, except in the
        /// greylist answer, which carries <c>Message</c> instead of a result.
        /// </summary>
        /// <returns>Why <paramref name="body"/> is not a validate result, or null.</returns>
        internal static string MissingValidateField(string body)
        {
            if (!(JToken.Parse(body) is JObject obj) || obj.ContainsKey("Message"))
                return null;
            foreach (var field in new[] { "address", "status" })
            {
                var value = obj[field];
                if (value == null || value.Type != JTokenType.String)
                    return "ZeroBounce returned " + Kind(value) + " for " + field + ", which is not a string.";
            }
            return null;
        }

        /// <summary>A string member: a JSON string or null, nothing else.</summary>
        internal sealed class StrictStringConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(string);

            public override bool CanWrite => false;

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) =>
                ReadString(reader);

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
                throw new NotSupportedException();
        }

        /// <summary>
        /// GetCredits' <c>Credits</c>: a JSON string holding a whole number, such as
        /// <c>"-1"</c> or <c>"12345"</c>. A bare number, <c>"abc"</c>, <c>"5.0"</c>,
        /// <c>null</c> or a missing field is an error.
        /// </summary>
        internal sealed class CreditsConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(int);

            public override bool CanWrite => false;

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var text = reader.TokenType == JsonToken.String ? (string)reader.Value : null;
                if (text != null && Integer.IsMatch(text)
                    && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var credits))
                    return credits;
                var found = Kind(reader.TokenType);
                reader.Skip();
                throw new ZBInvalidValueException("ZeroBounce returned " + found
                                                  + " for Credits, which is not a string holding a whole number.");
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
                throw new NotSupportedException();
        }
    }
}
