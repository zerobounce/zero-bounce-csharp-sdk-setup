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
            Converters = { new StrictStringConverter(), new NullAsZeroIntConverter(), new NullAsDefaultDateTimeConverter() },
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

        internal static string Kind(JToken token)
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
            return MissingString(obj, "");
        }

        /// <summary>
        /// Every <c>validatebatch</c> row is an object with <c>address</c> and
        /// <c>status</c> as strings.
        /// </summary>
        /// <returns>Why <paramref name="body"/> is not a batch result, or null.</returns>
        internal static string MissingBatchRowField(string body)
        {
            if (!(JToken.Parse(body) is JObject obj) || !(obj["email_batch"] is JArray rows))
                return null;
            foreach (var row in rows)
            {
                if (!(row is JObject rowObject))
                    return "ZeroBounce returned " + Kind(row) + " for a row of email_batch, which is not an object.";
                var missing = MissingString(rowObject, "email_batch row ");
                if (missing != null)
                    return missing;
            }
            return null;
        }

        private static string MissingString(JObject obj, string where)
        {
            foreach (var field in new[] { "address", "status" })
            {
                var value = obj[field];
                if (value == null || value.Type != JTokenType.String)
                    return "ZeroBounce returned " + Kind(value) + " for " + where + field + ", which is not a string.";
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
        /// An <c>int</c> member: null reads as 0, the empty value (standard.md section 3).
        /// Any other value is read as the default reader reads it.
        /// </summary>
        internal sealed class NullAsZeroIntConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(int);

            public override bool CanWrite => false;

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null || reader.TokenType == JsonToken.Undefined)
                    return 0;
                // Read the current token without advancing the reader (a converter must leave it on the value).
                if (reader.TokenType == JsonToken.Integer && reader.Value is long number
                    && number >= int.MinValue && number <= int.MaxValue)
                    return (int)number;
                if (reader.TokenType == JsonToken.Float && reader.Value is double fraction
                    && Math.Floor(fraction) == fraction && fraction >= int.MinValue && fraction <= int.MaxValue)
                    return (int)fraction;
                var text = reader.TokenType == JsonToken.String ? (string)reader.Value : null;
                if (text != null && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole))
                    return whole;
                var found = Kind(reader.TokenType);
                var field = FieldName(reader.Path);
                reader.Skip();
                throw new ZBInvalidValueException("ZeroBounce returned " + found + " for " + field
                                                  + ", which is not a whole number.");
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
                throw new NotSupportedException();
        }

        private static readonly Regex DashDate = new Regex(
            @"^([0-9]{4})-([0-9]{2})-([0-9]{2}) ([0-9]{2}):([0-9]{2}):([0-9]{2})\.([0-9]{3})$", RegexOptions.CultureInvariant);

        private static readonly Regex UsDate = new Regex(
            @"^([0-9]{1,2})/([0-9]{1,2})/([0-9]{4}) ([0-9]{1,2}):([0-9]{2}):([0-9]{2}) (AM|PM)$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex IsoDate = new Regex(
            @"^([0-9]{4})-([0-9]{2})-([0-9]{2})T([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.([0-9]{1,9}))?"
            + @"(Z|[+-][0-9]{2}(?::[0-9]{2}|[0-9]{2})?)(?:\[[A-Za-z][A-Za-z0-9_/+~.-]*\])?$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Reads a date the way Java 3.1.0's GsonDateDeserializer does, and no other way:
        /// <c>yyyy-MM-dd HH:mm:ss.SSS</c> and <c>M/d/yyyy h:mm:ss a</c> (AM/PM in any case)
        /// with no zone, read in the system zone (a <see cref="DateTimeKind.Local"/> result),
        /// and ISO 8601 <c>yyyy-MM-ddTHH:mm:ss[.f to 9 digits]</c> with an offset written as
        /// <c>Z</c>, <c>+hh:mm</c>, <c>+hhmm</c> or <c>+hh</c> and an optional <c>[Region/Id]</c>
        /// suffix (a <see cref="DateTimeKind.Utc"/> result). A field out of range (month 13,
        /// 30 February, hour 24) fails, as does any other text. Returns false when unreadable.
        /// </summary>
        internal static bool TryParseDate(string text, out DateTime result)
        {
            result = default(DateTime);
            try
            {
                var m = DashDate.Match(text);
                if (m.Success)
                {
                    result = new DateTime(Int(m, 1), Int(m, 2), Int(m, 3), Int(m, 4), Int(m, 5), Int(m, 6), Int(m, 7), DateTimeKind.Local);
                    return true;
                }
                m = UsDate.Match(text);
                if (m.Success)
                {
                    var hour = Int(m, 4);
                    if (hour < 1 || hour > 12) return false;
                    var pm = string.Equals(m.Groups[7].Value, "PM", StringComparison.OrdinalIgnoreCase);
                    result = new DateTime(Int(m, 3), Int(m, 1), Int(m, 2), hour % 12 + (pm ? 12 : 0), Int(m, 5), Int(m, 6), DateTimeKind.Local);
                    return true;
                }
                m = IsoDate.Match(text);
                if (m.Success)
                {
                    var local = new DateTime(Int(m, 1), Int(m, 2), Int(m, 3), Int(m, 4), Int(m, 5), Int(m, 6), DateTimeKind.Unspecified);
                    if (m.Groups[7].Success)
                    {
                        var digits = (m.Groups[7].Value + "000000000").Substring(0, 7);
                        local = local.AddTicks(long.Parse(digits, CultureInfo.InvariantCulture));
                    }
                    var offset = TimeSpan.Zero;
                    var zone = m.Groups[8].Value;
                    if (zone != "Z")
                    {
                        var digitsOnly = zone.Substring(1).Replace(":", "");
                        var hours = int.Parse(digitsOnly.Substring(0, 2), CultureInfo.InvariantCulture);
                        var minutes = digitsOnly.Length > 2 ? int.Parse(digitsOnly.Substring(2), CultureInfo.InvariantCulture) : 0;
                        if (hours > 18 || minutes > 59 || (hours == 18 && minutes > 0)) return false;
                        offset = new TimeSpan(hours, minutes, 0);
                        if (zone[0] == '-') offset = offset.Negate();
                    }
                    result = DateTime.SpecifyKind(local - offset, DateTimeKind.Utc);
                    return true;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            return false;
        }

        private static int Int(Match match, int group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);

        /// <summary>
        /// A <see cref="DateTime"/> or <c>DateTime?</c> member (Java 3.1.0's GsonDateDeserializer):
        /// null, a missing field and a blank string read as no date (null), and text in one of
        /// the formats of <see cref="TryParseDate"/> as that date. Anything else is an error.
        /// </summary>
        internal sealed class NullAsDefaultDateTimeConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(DateTime) || objectType == typeof(DateTime?);

            public override bool CanWrite => false;

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null || reader.TokenType == JsonToken.Undefined)
                    return objectType == typeof(DateTime?) ? null : (object)default(DateTime);
                var text = reader.TokenType == JsonToken.String ? (string)reader.Value : null;
                if (text != null && text.Trim().Length == 0)
                    return objectType == typeof(DateTime?) ? null : (object)default(DateTime);
                if (text != null && TryParseDate(text, out var parsed))
                    return parsed;
                var found = Kind(reader.TokenType);
                var field = FieldName(reader.Path);
                reader.Skip();
                throw new ZBInvalidValueException("ZeroBounce returned " + found + " for " + field
                                                  + ", which is not a date.");
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
                throw new NotSupportedException();
        }

        /// <summary>
        /// The AI score: a whole number in the <c>int</c> range, sent as a JSON integer or a
        /// JSON string holding one. null reads as 0. A fraction, an exponent (<c>5.0</c>,
        /// <c>1e2</c>), a string with anything else in it (<c>" 9"</c>, <c>"+9"</c>), a value
        /// outside the <c>int</c> range, a boolean, an array or an object is an error.
        /// </summary>
        internal sealed class IntegerConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(int);

            public override bool CanWrite => false;

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null || reader.TokenType == JsonToken.Undefined)
                    return 0;
                if (reader.TokenType == JsonToken.Integer && reader.Value is long number
                    && number >= int.MinValue && number <= int.MaxValue)
                    return (int)number;
                var text = reader.TokenType == JsonToken.String ? (string)reader.Value : null;
                if (text != null && Integer.IsMatch(text)
                    && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var score))
                    return score;
                var found = Kind(reader.TokenType);
                var field = FieldName(reader.Path);
                reader.Skip();
                throw new ZBInvalidValueException("ZeroBounce returned " + found + " for " + field
                                                  + ", which is not a whole number in the int range.");
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
                throw new NotSupportedException();
        }

        /// <summary>
        /// GetCredits' <c>Credits</c>: a whole number in the <c>int</c> range, sent as a
        /// JSON string (<c>"-1"</c>, <c>"12345"</c>, what the live API sends) or a JSON
        /// integer (<c>2375323</c>, zerobounce.net's own example). A fractional
        /// number, <c>"abc"</c>, <c>"5.0"</c>, a number outside the <c>int</c> range,
        /// <c>null</c> or a missing field is an error.
        /// </summary>
        internal sealed class CreditsConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(int);

            public override bool CanWrite => false;

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Integer && reader.Value is long number
                    && number >= int.MinValue && number <= int.MaxValue)
                    return (int)number;
                var text = reader.TokenType == JsonToken.String ? (string)reader.Value : null;
                if (text != null && Integer.IsMatch(text)
                    && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var credits))
                    return credits;
                var found = Kind(reader.TokenType);
                reader.Skip();
                throw new ZBInvalidValueException("ZeroBounce returned " + found
                                                  + " for Credits, which is not a whole number in the int range.");
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
                throw new NotSupportedException();
        }
    }
}
