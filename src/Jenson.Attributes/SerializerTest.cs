using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jenson.Attributes
{
    public class SerializerTest : System.Text.Json.Serialization.JsonConverter<string>
    {
        [JsonPropertyName("propname")]
        public string PropName { get; set; }

        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();


            var length = 0;
            reader.Read();
            while (reader.TokenType != System.Text.Json.JsonTokenType.EndArray)
            {
                if (reader.TokenType != System.Text.Json.JsonTokenType.Comment)
                    length++;
                reader.Skip();
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
