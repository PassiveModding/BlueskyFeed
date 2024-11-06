using BlueskyFeed.Api.Services;
using BlueskyFeed.Api.Util;

namespace BlueskyFeed.Api.Generator;

public class TestFeedGenerator : IAuthorizedFeedGenerator
{
    private readonly ILogger<TestFeedGenerator> _logger;
    private readonly FollowHelper _followHelper;
    private readonly RedisHelper _redisHelper;

    public TestFeedGenerator(ILogger<TestFeedGenerator> logger, 
        FollowHelper followHelper,
        RedisHelper redisHelper)
    {
        _logger = logger;
        _followHelper = followHelper;
        _redisHelper = redisHelper;
    }
    
    public string GetUri(string handler) => $"at://{handler}/app.bsky.feed.generator/test-feed";

    public async Task<FeedResponse> RetrieveAsync(string? cursor, int limit,
        string issuerDid, CancellationToken cancellationToken)
    {
        var profiles = await _followHelper.GetFollowing(issuerDid, cancellationToken);
        var handles = profiles.Select(x => x.Did.Handler).ToHashSet();
        var (newCursor, parsedResults) = await _redisHelper.GetLikesByHandles(cursor, limit, handles);
        return LikedFeedUtil.ConstructFeedResponse(newCursor, parsedResults, profiles);
    }
}