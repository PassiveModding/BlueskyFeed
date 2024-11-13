using FishyFlip.Models;
using MongoDB.Bson.Serialization.Attributes;

namespace BlueskyFeed.Common.Db;

public class LikeRecord
{
    [BsonId]
    public string Id => $"{Handler}:{RKey}";
    
    [BsonElement("handler")]
    public string Handler { get; init; }
    
    [BsonElement("rkey")]
    public string RKey { get; init; }
    
    [BsonElement("subjectUri")]
    public string SubjectUri { get; init; }
    
    [BsonElement("createdAt"), BsonRepresentation(MongoDB.Bson.BsonType.DateTime)]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CreatedAt { get; init; }
    
    [BsonElement("indexedAt"), BsonRepresentation(MongoDB.Bson.BsonType.DateTime)]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime IndexedAt { get; init; }
    
    public Cursor Cursor => new Cursor(((DateTimeOffset)IndexedAt).ToUnixTimeMilliseconds(), RKey);

    public LikeRecord(string handle, string rkey, Like like)
    {
        Handler = handle;
        RKey = rkey;
        SubjectUri = like.Subject?.Uri?.ToString() ?? throw new ArgumentNullException(nameof(like.Subject.Uri));
        CreatedAt = like.CreatedAt;
        IndexedAt = DateTime.UtcNow;
    }
}