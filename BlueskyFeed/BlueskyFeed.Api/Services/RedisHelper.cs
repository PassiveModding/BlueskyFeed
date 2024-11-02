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
        var cursorTimestamp = DateTimeOffset.FromUnixTimeSeconds(cursorValue?.Timestamp ?? 0).ToUnixTimeSeconds();
        var cursorRKey = cursorValue?.RKey;
        
        // paged get likes descending from cursor
        var matchingKeys = new List<Key>();
        var scanned = 0;
        do
        {
            var keys = await _database.SortedSetRangeByScoreWithScoresAsync(
                key: Constants.FeedType.Like,
                start: cursorTimestamp,
                stop: double.PositiveInfinity,
                Exclude.Start,
                take: 10000,
                order: Order.Descending);

            scanned += keys.Length;
            if (keys.Length == 0)
            {
                break;
            }
            
            var keysParsed = keys
                .Where(x => x.Element.HasValue)
                .Select(x => Key.Parse(x.Element!))
                .ToArray();
            
            matchingKeys.AddRange(keysParsed.Where(x => handles.Contains(x.Handler)));
            var lastKey = keys[^1];
            cursorTimestamp = (long) lastKey.Score;
            cursorRKey = Key.Parse(lastKey.Element!).RKey;
        }
        while (matchingKeys.Count < limit);
        
        _logger.LogInformation("Scanned {Scanned} keys", scanned);
        
        matchingKeys = matchingKeys.Where(x => x.RKey != cursorValue?.RKey).ToList();
        var newCursor = new Cursor(cursorTimestamp, cursorRKey ?? "");
        
        var parsedResults = new List<(Key Key, Like Like)>();
        foreach (var key in matchingKeys)
        {
            var like = await _database.StringGetAsync(key.ToString());
            if (!like.HasValue) continue;
            var likeParsed = JsonSerializer.Deserialize<Like>(like!, Entities.JsonSerializerOptions);
            if (likeParsed != null)
            {
                parsedResults.Add((key, likeParsed));
            }
        }
        
        return (newCursor, parsedResults);
    }
}