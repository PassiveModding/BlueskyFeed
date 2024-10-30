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

    public record Key(string Collection, string Handler, string RKey)
    {
        // handler contains : so we use :: as separator
        public override string ToString() => $"{Collection}::{Handler}::{RKey}";
        public static Key Parse(string key)
        {
            var parts = key.Split("::");
            return new Key(parts[0], parts[1], parts[2]);
        }
    }
    
    public record FollowKey(string IssuerDid)
    {
        public override string ToString() => $"follow::{IssuerDid}";
        public static FollowKey Parse(string key)
        {
            var parts = key.Split("::");
            return new FollowKey(parts[1]);
        }
    }
    
    public record FollowingKey(string IssuerDid)
    {
        public override string ToString() => $"following::{IssuerDid}";
        public static FollowingKey Parse(string key)
        {
            var parts = key.Split("::");
            return new FollowingKey(parts[1]);
        }
    }
    
    public record ProfileKey(string Handler)
    {
        public override string ToString() => $"profile::{Handler}";
        public static ProfileKey Parse(string key)
        {
            var parts = key.Split("::");
            return new ProfileKey(parts[1]);
        }
    }
    
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