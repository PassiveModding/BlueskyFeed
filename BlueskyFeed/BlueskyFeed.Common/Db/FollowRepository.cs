using FishyFlip.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace BlueskyFeed.Common.Db;

public class FollowRepository
{
    private readonly ILogger<FollowRepository> _logger;
    private readonly MongoDbService _mongoDb;
    private const string CollectionFollowers = "followers";
    private const string CollectionFollowing = "following";

    public FollowRepository(ILogger<FollowRepository> logger, MongoDbService mongoDb)
    {
        _logger = logger;
        _mongoDb = mongoDb;
        
        try
        {
            var collection = _mongoDb.GetDatabase().GetCollection<FollowRecord>(CollectionFollowers);
            var index = Builders<FollowRecord>.IndexKeys.Ascending(x => x.Created);
            var options = new CreateIndexOptions
            {
                ExpireAfter = TimeSpan.FromHours(1)
            };
            var model = new CreateIndexModel<FollowRecord>(index, options);
            collection.Indexes.CreateOne(model);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create TTL index on followers collection");
        }
        
        try
        {
            var collection = _mongoDb.GetDatabase().GetCollection<FollowRecord>(CollectionFollowing);
            var index = Builders<FollowRecord>.IndexKeys.Ascending(x => x.Created);
            var options = new CreateIndexOptions
            {
                ExpireAfter = TimeSpan.FromHours(1)
            };
            var model = new CreateIndexModel<FollowRecord>(index, options);
            collection.Indexes.CreateOne(model);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create TTL index on following collection");
        }
    }
    
    public async Task SetFollowersAsync(string didBeingFollowed, FeedProfile[] followers)
    {
        var collection = _mongoDb.GetDatabase().GetCollection<FollowRecord>(CollectionFollowers);
        var record = new FollowRecord
        {
            Did = didBeingFollowed,
            Followers = followers.Select(x => new FeedProfileRecord(x.Did.Handler, x.DisplayName, x.Handle)).ToArray()
        };
        
        await collection.ReplaceOneAsync(x => x.Did == didBeingFollowed, record, new ReplaceOptions { IsUpsert = true });
    }
    
    public async Task SetFollowingAsync(string didFollowing, FeedProfile[] following)
    {
        var collection = _mongoDb.GetDatabase().GetCollection<FollowRecord>(CollectionFollowing);
        var record = new FollowRecord
        {
            Did = didFollowing,
            Followers = following.Select(x => new FeedProfileRecord(x.Did.Handler, x.DisplayName, x.Handle)).ToArray()
        };
        
        await collection.ReplaceOneAsync(x => x.Did == didFollowing, record, new ReplaceOptions { IsUpsert = true });
    }

    public async Task<FeedProfileRecord[]?> GetFollowersAsync(string didBeingFollowed)
    {
        var collection = _mongoDb.GetDatabase().GetCollection<FollowRecord>(CollectionFollowers);
        var record = await collection.Find(x => x.Did == didBeingFollowed).FirstOrDefaultAsync();
        return record?.Followers ?? null;
    }
    
    public async Task<FeedProfileRecord[]?> GetFollowingAsync(string didFollowing)
    {
        var collection = _mongoDb.GetDatabase().GetCollection<FollowRecord>(CollectionFollowing);
        var record = await collection.Find(x => x.Did == didFollowing).FirstOrDefaultAsync();
        return record?.Followers ?? null;
    }
}

public class FollowRecord
{
    [BsonId]
    [BsonElement("did")]
    public string Did { get; init; }
    
    [BsonElement("followers")]
    public FeedProfileRecord[] Followers { get; init; }
    
    [BsonElement("created")]
    public DateTime Created { get; init; } = DateTime.UtcNow;
}

public record FeedProfileRecord(string Did, string DisplayName, string Handle);
