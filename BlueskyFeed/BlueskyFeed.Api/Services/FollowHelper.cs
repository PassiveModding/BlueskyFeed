using BlueskyFeed.Api.Util;
using BlueskyFeed.Common;
using FishyFlip.Models;
using StackExchange.Redis;

namespace BlueskyFeed.Api.Services;

public class FollowHelper : IService
{
    private readonly SessionService _sessionService;
    private readonly ILogger<FollowHelper> _logger;
    private readonly IDatabase _database;

    public FollowHelper(SessionService sessionService, IConnectionMultiplexer connectionMultiplexer, ILogger<FollowHelper> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
        _database = connectionMultiplexer.GetDatabase();
    }
    
    public async Task<FeedProfile[]> GetFollowers(string issuerDid, CancellationToken cancellationToken)
    {
        using var activity = DiagnosticsConfig.Source.StartActivity()
            .WithIssuerDid(issuerDid);
        
        var proto = _sessionService.GetProtocol();

        // check redis
        var followKey = new FollowKey(issuerDid);
        if (await _database.KeyExistsAsync(followKey.ToString()))
        {
            var cachedFollowing = await _database.SetMembersAsync(followKey.ToString());
            activity?.WithIsCached(true)
                .WithFollowerCount(cachedFollowing.Length);
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
        
        _logger.LogInformation("Getting followers for {Did}", issuerDid);
        var followers = new List<FeedProfile>();
        string? followCursor = null;
        do
        {
            var result = await proto.Graph.GetFollowersAsync(identifier, limit: 100, cursor: followCursor, cancellationToken: cancellationToken);
            var followRecord = result.AsT0 ?? throw new Exception("Failed to get actor feeds");
            var followerSet = followRecord.Followers ?? throw new Exception("Missing followers");
            followers.AddRange(followerSet);
            followCursor = followRecord.Cursor;
        } while (followCursor != null);
        
        // insert followers
        var followerTasks = followers.Select(x => _database.SetAddAsync(followKey.ToString(), x.ToBlob()));
        await Task.WhenAll(followerTasks);
        await _database.KeyExpireAsync(followKey.ToString(), TimeSpan.FromHours(1));
        activity?.WithIsCached(false)
            .WithFollowerCount(followers.Count);
        
        return followers.ToArray();
    }
    
    public async Task<FeedProfile[]> GetFollowing(string issuerDid, CancellationToken cancellationToken)
    {
        using var activity = DiagnosticsConfig.Source.StartActivity()
            .WithIssuerDid(issuerDid);
        
        var proto = _sessionService.GetProtocol();

        // check redis
        var followingKey = new FollowingKey(issuerDid);
        if (await _database.KeyExistsAsync(followingKey.ToString()))
        {
            var cachedFollowing = await _database.SetMembersAsync(followingKey.ToString());
            activity?.WithIsCached(true)
                .WithFollowingCount(cachedFollowing.Length);
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
        
        _logger.LogInformation("Getting following for {Did}", issuerDid);
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
        activity?.WithIsCached(false)
            .WithFollowingCount(following.Count);
        
        return following.ToArray();
    }
}