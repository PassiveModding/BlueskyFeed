using System.Text.Json;
using System.Text.Json.Serialization;
using FishyFlip.Models;
using Ipfs;

namespace BlueskyFeed.Common;

public class Entities
{
    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new CidConverter(), new AtUriConverter() }
    };
    
    private class AtUriConverter : JsonConverter<ATUri?>
    {
        public override ATUri? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var content = reader.GetString();
                if (content != null)
                {
                    return new ATUri(content);
                }
            }
            return null;
        }

        public override void Write(Utf8JsonWriter writer, ATUri? value, JsonSerializerOptions options)
        {
            if (value != null)
            {
                writer.WriteStringValue(value.ToString());
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
    
    private class CidConverter : JsonConverter<Cid?>
    {
        public override Cid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var content = reader.GetString();
                if (content != null)
                {
                    return Cid.Decode(content);
                }
            }
            return null;
        }

        public override void Write(Utf8JsonWriter writer, Cid? value, JsonSerializerOptions options)
        {
            if (value != null)
            {
                writer.WriteStringValue(value.ToString());
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}