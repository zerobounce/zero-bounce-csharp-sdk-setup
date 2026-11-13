using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ZeroBounceSDK
{
    public class ZBResponse
    {
        public class SingleOrArrayConverter<T> : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return (objectType == typeof(List<T>));
            }
        
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var path = reader.Path;
                var token = JToken.Load(reader);
                // An item of the list is a value, not an optional field: null is not one (E7).
                if (token is JArray array && array.Any(item => item.Type == JTokenType.Null))
                    throw new ZBInvalidValueException("ZeroBounce returned null in " + ZBStrictTypes.FieldName(path)
                                                      + ", which is not a string.");
                // With the caller's serializer, so its strict converters apply (E7).
                return token.Type == JTokenType.Array 
                    ? token.ToObject<List<T>>(serializer) 
                    : new List<T> { token.ToObject<T>(serializer) };
            }
        
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var list = (List<T>)value;
                if (list.Count == 1)
                {
                    value = list[0];
                }
                serializer.Serialize(writer, value);
            }
        
            public override bool CanWrite => true;
        }

        /// <summary>
        /// A file-job message the public type keeps as one string: a JSON string, null, or
        /// a list of strings joined with newlines (standard.md section 3). A list item must
        /// be a string; null or any other value is an error with status 200.
        /// </summary>
        public class MessageTextConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(string);

            public override bool CanWrite => false;

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var token = JToken.Load(reader);
                if (token.Type == JTokenType.String)
                    return token.Value<string>();
                if (token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                    return null;
                if (token is JArray array)
                {
                    foreach (var item in array)
                        if (item.Type != JTokenType.String)
                            throw new ZBInvalidValueException("ZeroBounce returned " + ZBStrictTypes.Kind(item)
                                                              + " in message, which is not a string.");
                    return string.Join("\n", array.Select(item => item.Value<string>()));
                }
                throw new ZBInvalidValueException("ZeroBounce returned " + ZBStrictTypes.Kind(token)
                                                  + " for message, which is not a string or a list of strings.");
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
                throw new NotSupportedException();
        }

    }
}