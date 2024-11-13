using FishyFlip;
using FishyFlip.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace BlueskyFeed.Common.Db;

public partial class LikeRepository
{
    private readonly ILogger<LikeRepository> _logger;
    private readonly MongoDbService _mongoDb;
    private const string Collection = Constants.FeedType.Like;
    
    public LikeRepository(ILogger<LikeRepository> logger, MongoDbService mongoDb)
    {
        _logger = logger;
        _mongoDb = mongoDb;
        
        // expire entries based on their IndexedAt timestamp, max 1min
        try
        {
            var index = Builders<LikeRecord>.IndexKeys.Ascending(x => x.IndexedAt);
            var options = new CreateIndexOptions
            {
                ExpireAfter = TimeSpan.FromHours(24)
            };
            var model = new CreateIndexModel<LikeRecord>(index, options);
            var collection = _mongoDb.GetDatabase().GetCollection<LikeRecord>(Collection);
            collection.Indexes.CreateOne(model);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create TTL index on likes collection");
        }
        
        // create unique index on handler and rkey
        try
        {
            var index = Builders<LikeRecord>.IndexKeys.Ascending(x => x.Handler).Ascending(x => x.RKey);
            var options = new CreateIndexOptions
            {
                Unique = true
            };
            var model = new CreateIndexModel<LikeRecord>(index, options);
            var collection = _mongoDb.GetDatabase().GetCollection<LikeRecord>(Collection);
            collection.Indexes.CreateOne(model);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create unique index on likes collection");
        }
    }
    
    public async Task AddLikeAsync(string handle, string rkey, Like like)
    {
        var record = new LikeRecord(handle, rkey, like);
        var collection = _mongoDb.GetDatabase().GetCollection<LikeRecord>(Collection);
        await collection.InsertOneAsync(record);
    }
    
    public async Task RemoveLikeAsync(string handle, string rkey)
    {
        var collection = _mongoDb.GetDatabase().GetCollection<LikeRecord>(Collection);
        var filter = Builders<LikeRecord>.Filter.Eq(x => x.Handler, handle) & 
                     Builders<LikeRecord>.Filter.Eq(x => x.RKey, rkey);
        await collection.DeleteOneAsync(filter);
    }
    
    public async Task<LikeRecord[]> GetLikesByHandlesAsync(IEnumerable<string> handles, int limit, string? cursor = null)
    {
        var handleSet = handles.ToHashSet();
        var collection = _mongoDb.GetDatabase().GetCollection<LikeRecord>(Collection);
        var filter = Builders<LikeRecord>.Filter.In(x => x.Handler, handleSet);
        if (cursor != null)
        {
            var cursorParts = Cursor.FromString(cursor);
            var dateTimeTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(cursorParts!.Timestamp).UtcDateTime;
            var cursorFilter = Builders<LikeRecord>.Filter.Lt(x => x.IndexedAt, dateTimeTimestamp);
            filter &= cursorFilter;
        }
        
        var sort = Builders<LikeRecord>.Sort.Descending(x => x.IndexedAt).Ascending(x => x.RKey);
        var query = collection.Find(filter).Sort(sort).Limit(limit);
        
        var result = await query.ToListAsync();
        return result.ToArray();
    }
}