using System.Text.Json;
using BlueskyFeed.Common;
using FishyFlip;
using FishyFlip.Models;
using StackExchange.Redis;

namespace BlueskyFeed.Api.Services;

public class RedisHelper : IService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisHelper> _logger;

    public RedisHelper(ILogger<RedisHelper> logger, 
        IConnectionMultiplexer connectionMultiplexer)
    {
        _logger = logger;
        _database = connectionMultiplexer.GetDatabase();
    }

   
    public async Task<(Cursor newCursor, List<(Key Key, Like Like)> parsedResults)> GetLikesByHandles(string? cursor, int limit, IEnumerable<string> handles)
    {
        var cursorValue = Cursor.FromString(cursor);
        var cursorTimestamp = cursorValue?.Timestamp ?? long.MaxValue;
        var cursorRKey = cursorValue?.RKey;
        
        var matchingKeys = new List<(Key Key, long score)>();
        var scanned = 0;
        var lastProcessedTimestamp = cursorTimestamp;
        var chunkSize = 10000;
        do
        {
            var keys = await _database.SortedSetRangeByScoreWithScoresAsync(
                key: Constants.FeedType.Like,
                start: long.MinValue,
                stop: lastProcessedTimestamp,
                exclude: Exclude.Stop,
                take: chunkSize,
                order: Order.Descending);

            scanned += keys.Length;
            if (keys.Length == 0)
            {
                break;
            }
            
            var keysParsed = keys
                .Where(x => x.Element.HasValue)
                .Select(x => (Key: Key.Parse(x.Element!), x.Score))
                .ToArray();
            
            matchingKeys.AddRange(keysParsed
                .Where(x => handles.Contains(x.Key.Handler) && x.Key.RKey != cursorRKey)
                .Select(x => (x.Key, (long)x.Score)));
            var lastKey = keys.Last();
            lastProcessedTimestamp = (long) lastKey.Score;
            cursorRKey = Key.Parse(lastKey.Element!).RKey;
        }
        while (matchingKeys.Count < limit);
        
        _logger.LogInformation("Scanned {Scanned} keys", scanned);
        _logger.LogInformation("Found {Count} likes", matchingKeys.Count);
        
        if (matchingKeys.Count == 0)
        {
            return (new Cursor(0, ""), []);
        }
        
        var matching = matchingKeys
            .OrderByDescending(x => x.score)
            .Take(limit)
            .ToArray();
        
        var lastKeyProcessed = matching.Last();
        var newCursor = new Cursor(lastKeyProcessed.score, lastKeyProcessed.Key.RKey);
        var parsedResults = new List<(Key Key, Like Like)>();
        foreach (var key in matchingKeys)
        {
            var like = await _database.StringGetAsync(key.Key.ToString());
            if (!like.HasValue) continue;
            var likeParsed = JsonSerializer.Deserialize<Like>(like!, Entities.JsonSerializerOptions);
            if (likeParsed != null)
            {
                parsedResults.Add((key.Key, likeParsed));
            }
        }
        
        return (newCursor, parsedResults);
    }
}