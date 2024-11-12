using BlueskyFeed.Api.Services;
using BlueskyFeed.Api.Util;
using BlueskyFeed.Common;
using BlueskyFeed.Common.Db;

namespace BlueskyFeed.Api.Generator;

public class LikedByMutualsFeedGenerator : IAuthorizedFeedGenerator
{
    private readonly ILogger<LikedByMutualsFeedGenerator> _logger;
    private readonly FollowHelper _followHelper;
    private readonly FeedRepository _feedRepository;

    public LikedByMutualsFeedGenerator(ILogger<LikedByMutualsFeedGenerator> logger, 
        FollowHelper followHelper,
        FeedRepository feedRepository)
    {
        _logger = logger;
        _followHelper = followHelper;
        _feedRepository = feedRepository;
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
        var results = await _feedRepository.GetLikesByHandlesAsync(mutualHandles, limit, cursor);
        var newCursor = results.LastOrDefault()?.Cursor ?? Cursor.Empty;        
        if (newCursor.ToString() == cursor)
        {
            return new FeedResponse(Cursor.Empty.ToString(), []);
        }
        
        return LikedFeedUtil.ConstructFeedResponse(newCursor, results, allProfiles);
    }
}