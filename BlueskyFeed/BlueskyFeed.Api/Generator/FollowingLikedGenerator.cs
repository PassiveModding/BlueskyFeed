using System.Globalization;
using System.Text.Json;
using BlueskyFeed.Api.Services;
using BlueskyFeed.Api.Util;
using BlueskyFeed.Common;
using FishyFlip.Models;
using StackExchange.Redis;

namespace BlueskyFeed.Api.Generator;

public class FollowingLikedGenerator : IAuthorizedFeedGenerator
{
    private readonly ILogger<FollowersLikedGenerator> _logger;
    private readonly SessionService _sessionService;
    private readonly IDatabase _database;

    public FollowingLikedGenerator(ILogger<FollowersLikedGenerator> logger, IConnectionMultiplexer connectionMultiplexer, SessionService sessionService)
    {
        _logger = logger;
        _sessionService = sessionService;
        _database = connectionMultiplexer.GetDatabase();
    }
    
    public string GetUri(string handler) => $"at://{handler}/app.bsky.feed.generator/following-liked";

    public async Task<FeedResponse> RetrieveAsync(string? cursor, int limit,
        string issuerDid, CancellationToken cancellationToken)
    {
        var following = await GetFollowing(issuerDid, cancellationToken);
        var followingDidHandles = following.Select(x => x.Did.Handler).ToHashSet();
        var (newCursor, results) = await GetEntries(cursor, limit, followingDidHandles, cancellationToken);
        
        return new FeedResponse(newCursor.ToString(CultureInfo.InvariantCulture), results
            .Where(x => x.Like.Subject?.Uri != null)
            .Select(x =>
            {
                var profile = following.FirstOrDefault(y => y.Did.Handler == x.Key.Handler);
                return new FeedResponseRecord(x.Like.Subject!.Uri!.ToString(), $"Liked by {profile?.DisplayName} ({profile?.Handle})");
            })
            .ToArray());
    }

    private async Task<(long Cursor, List<(Entities.Key Key, Like Like)> Likes)> GetEntries(string? cursor, int limit, IReadOnlySet<string> followingDidHandles, CancellationToken cancellationToken)
    {
        var results = new List<(Entities.Key Key, Like Like)>();
        
        // cursor is DateTime.Ticks at the time the like was indexed, want to take posts from cursor to x
        var currentCursor = cursor != null && long.TryParse(cursor, out var cursorScore) 
            ? cursorScore 
            : long.MaxValue;

        var batchSize = Math.Min(limit * 3, 100); // Fetch more items to account for filtering, but cap it

        var lastCursor = currentCursor;
        while (results.Count < limit && lastCursor > 0)
        {
            // Get next batch of entries
            var entries = await _database.SortedSetRangeByScoreWithScoresAsync(
                "likes",
                start: double.NegativeInfinity,
                stop: lastCursor,
                exclude: Exclude.Stop,
                order: Order.Descending,
                take: batchSize);

            if (entries.Length == 0) break;

            // Process entries
            foreach (var entry in entries)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return (lastCursor, results);
                }

                var key = entry.Element.ToString()!;
                var parsedKey = Entities.Key.Parse(key);
                if (SetContainsKeyHandler(parsedKey, followingDidHandles))
                {
                    var value = await _database.StringGetAsync(key);
                    if (value.HasValue)
                    {
                        var like = JsonSerializer.Deserialize<Like>(value!, Common.Entities.JsonSerializerOptions);
                        if (like != null)
                        {
                            results.Add((parsedKey, like));

                            if (results.Count == limit)
                            {
                                lastCursor = (long) entry.Score;
                                break;
                            }
                        }
                    }
                }

                lastCursor = (long) entry.Score;
            }
        }
        
        return (lastCursor, results);
    }

    private bool SetContainsKeyHandler(Entities.Key key, IReadOnlySet<string> handlers)
    {
        return handlers.Contains(key.Handler);
    }
        
    private async Task<FeedProfile[]> GetFollowing(string issuerDid, CancellationToken cancellationToken)
    {
        var proto = _sessionService.GetProtocol();

        // check redis
        var followingKey = new Entities.FollowingKey(issuerDid);
        if (await _database.KeyExistsAsync(followingKey.ToString()))
        {
            var cachedFollowing = await _database.SetMembersAsync(followingKey.ToString());
            return cachedFollowing
                .Where(x => x.HasValue)
                .Select(x => FollowUtil.ParseFeedProfileBlob(x!))
                .ToArray();
        }
        
        var identifier = ATIdentifier.Create(issuerDid);
        if (identifier == null)
        {
            throw new Exception("Invalid issuer DID");
        }
        
        var following = new List<FeedProfile>();
        string? followingCursor = null;
        do
        {
            var result = await proto.Graph.GetFollowsAsync(identifier, limit: 100, cursor: followingCursor, cancellationToken: cancellationToken);
            var followRecord = result.AsT0 ?? throw new Exception("Failed to get actor feeds");
            var followings = followRecord.Follows ?? throw new Exception("Missing following");
            following.AddRange(followings);
            followingCursor = followRecord.Cursor;
        } while (followingCursor != null);
        
        // insert following
        var followingTasks = following.Select(x => _database.SetAddAsync(followingKey.ToString(), x.ToBlob()));
        await Task.WhenAll(followingTasks);
        await _database.KeyExpireAsync(followingKey.ToString(), TimeSpan.FromHours(1));
        
        return following.ToArray();
    }
}