using FishyFlip;
using FishyFlip.Models;
using NRediSearch;

namespace BlueskyFeed.Common.Db;

public class LikeRecord
{
    public const string Collection = Constants.FeedType.Like;

    public static string ConstructKey(string handle, string rkey) => $"{Collection}:{handle}:{rkey}";
    public string Id => ConstructKey(Plc, RKey);
    public string GetDid() => $"did:plc:{Plc}";
    public long Score => ((DateTimeOffset) IndexedAt).ToUnixTimeMilliseconds();
    public string Plc { get; init; }
    public string RKey { get; init; }
    public string SubjectUri { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime IndexedAt { get; init; }
    
    public Cursor Cursor => new Cursor(((DateTimeOffset)IndexedAt).ToUnixTimeMilliseconds(), RKey);

    public LikeRecord(string handle, string rkey, Like like)
    {
        if (!FeedRepository.PlcHandleRegex().IsMatch(handle))
        {
            throw new ArgumentException($"Handle must start with 'did:plc:' but was {handle}", nameof(handle));
        }

        Plc = handle.Split(":").Last();
        RKey = rkey;
        SubjectUri = like.Subject?.Uri?.ToString() ?? throw new ArgumentNullException(nameof(like.Subject.Uri));
        CreatedAt = like.CreatedAt;
        IndexedAt = DateTime.UtcNow;
    }

    private LikeRecord(string plc, string rkey, string subjectUri, DateTime? createdAt, DateTime indexedAt)
    {
        Plc = plc;
        RKey = rkey;
        SubjectUri = subjectUri;
        CreatedAt = createdAt;
        IndexedAt = indexedAt;
    }

    public static bool CreateSchema(Client client)
    {
        var schema = new Schema()
            .AddTextField(nameof(Plc))
            .AddTextField(nameof(RKey))
            .AddTextField(nameof(SubjectUri))
            .AddNumericField(nameof(CreatedAt))
            //.AddNumericField(nameof(IndexedAt))
            .AddSortableNumericField(nameof(IndexedAt));

        var created = client.CreateIndex(schema, new Client.ConfiguredIndexOptions(
            new Client.IndexDefinition(prefixes: [Collection])
        ));

        return created;
    }

    public Document ToDocument()
    {
        var doc = new Document(Id);
        doc.Set(nameof(Plc), Plc);
        doc.Set(nameof(RKey), RKey);
        doc.Set(nameof(SubjectUri), SubjectUri);
        doc.Set(nameof(CreatedAt), ((DateTimeOffset?) CreatedAt)?.ToUnixTimeMilliseconds());
        doc.Set(nameof(IndexedAt), ((DateTimeOffset) IndexedAt).ToUnixTimeMilliseconds());
        return doc;
    }

    public static LikeRecord FromDocument(Document doc)
    {
        var plc = doc[nameof(Plc)].ToString();
        var rkey = doc[nameof(RKey)].ToString();
        var subjectUri = doc[nameof(SubjectUri)].ToString();
        var createdAt = doc[nameof(CreatedAt)].IsNullOrEmpty ? null : doc[nameof(CreatedAt)].ToString();
        var indexedAt = doc[nameof(IndexedAt)].ToString();

        var createdAtObj = createdAt == null
            ? null
            : (DateTime?) DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(createdAt)).UtcDateTime;
        var indexedAtObj = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(indexedAt)).UtcDateTime;
        return new LikeRecord(plc, rkey, subjectUri, createdAtObj, indexedAtObj);
    }
}