using System;
using Newtonsoft.Json;

namespace Timelapse.Util
{
    public static class JsonConverters
    {
        public class WhiteSpaceToNullConverter : JsonConverter
        {
            public override bool CanRead => true;
            public override bool CanWrite => false;

            public override bool CanConvert(Type objectType) => objectType == typeof(string);

            public override object ReadJson(JsonReader reader, Type objectType,
                object existingValue, JsonSerializer serializer)
            {
                return string.IsNullOrWhiteSpace((string)reader.Value) ? null : (string)reader.Value;
            }

            public override void WriteJson(JsonWriter writer, object value,
                JsonSerializer serializer)
            {
                // Not implemented as unused 
            }
        }
    }
}
