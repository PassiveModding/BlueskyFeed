using BlueskyFeed.Api.Services;
using BlueskyFeed.Api.Util;

namespace BlueskyFeed.Api.Generator;

public class LikedByMutualsFeedGenerator : IAuthorizedFeedGenerator
{
    private readonly ILogger<LikedByMutualsFeedGenerator> _logger;
    private readonly FollowHelper _followHelper;
    private readonly RedisHelper _redisHelper;

    public LikedByMutualsFeedGenerator(ILogger<LikedByMutualsFeedGenerator> logger, 
        FollowHelper followHelper,
        RedisHelper redisHelper)
    {
        _logger = logger;
        _followHelper = followHelper;
        _redisHelper = redisHelper;
    }
    
    public string GetUri(string handler) => $"at://{handler}/app.bsky.feed.generator/mutuals-liked";

    public async Task<FeedResponse> RetrieveAsync(string? cursor, int limit,
        string issuerDid, CancellationToken cancellationToken)
    {
        var following = await _followHelper.GetFollowing(issuerDid, cancellationToken);
        var followers = await _followHelper.GetFollowers(issuerDid, cancellationToken);
        var followingHandles = following.Select(x => x.Did.Handler).ToList();
        var followerHandles = followers.Select(x => x.Did.Handler).ToList();
        var mutualHandles = followingHandles.Intersect(followerHandles).ToList();
        var allProfiles = following.Concat(followers).DistinctBy(x => x.Did.Handler).ToArray();
        var (newCursor, parsedResults) = await _redisHelper.GetLikesByHandles(cursor, limit, mutualHandles);
        return LikedFeedUtil.ConstructFeedResponse(newCursor, parsedResults, allProfiles);
    }
}