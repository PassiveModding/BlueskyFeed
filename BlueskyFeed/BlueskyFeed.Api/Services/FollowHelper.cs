using BlueskyFeed.Common.Db;
using FishyFlip.Models;

namespace BlueskyFeed.Api.Services;

public class FollowHelper : IService
{
    private readonly SessionService _sessionService;
    private readonly ILogger<FollowHelper> _logger;
    private readonly FollowRepository _followRepository;

    public FollowHelper(SessionService sessionService, ILogger<FollowHelper> logger, FollowRepository followRepository)
    {
        _sessionService = sessionService;
        _logger = logger;
        _followRepository = followRepository;
    }
    
    public async Task<FeedProfileRecord[]> GetFollowers(string issuerDid, CancellationToken cancellationToken)
    {
        using var activity = DiagnosticsConfig.Source.StartActivity()
            .WithIssuerDid(issuerDid);
        
        var cachedFollowers = await _followRepository.GetFollowersAsync(issuerDid);
        if (cachedFollowers != null)
        {
            _logger.LogInformation("Using cached following for {Did}", issuerDid);
            return cachedFollowers;
        }
        
        var proto = _sessionService.GetProtocol();
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

        await _followRepository.SetFollowersAsync(issuerDid, followers.ToArray());
        return followers.Select(x => new FeedProfileRecord(x.Did.Handler, x.DisplayName, x.Handle)).ToArray();
    }
    
    public async Task<FeedProfileRecord[]> GetFollowing(string issuerDid, CancellationToken cancellationToken)
    {
        using var activity = DiagnosticsConfig.Source.StartActivity()
            .WithIssuerDid(issuerDid);
        
        var cachedFollowing = await _followRepository.GetFollowingAsync(issuerDid);
        if (cachedFollowing != null)
        {
            _logger.LogInformation("Using cached following for {Did}", issuerDid);
            return cachedFollowing;
        }
        
        var proto = _sessionService.GetProtocol();
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

        await _followRepository.SetFollowingAsync(issuerDid, following.ToArray());
        return following.Select(x => new FeedProfileRecord(x.Did.Handler, x.DisplayName, x.Handle)).ToArray();
    }
}