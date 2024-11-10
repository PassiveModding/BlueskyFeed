using BlueskyFeed.Api.Services;
using BlueskyFeed.Api.Util;
using BlueskyFeed.Common;
using BlueskyFeed.Common.Db;

namespace BlueskyFeed.Api.Generator;

public class LikedByFollowingFeedGenerator : IAuthorizedFeedGenerator
{
    private readonly ILogger<LikedByFollowingFeedGenerator> _logger;
    private readonly FollowHelper _followHelper;
    private readonly FeedRepository _feedRepository;

    public LikedByFollowingFeedGenerator(ILogger<LikedByFollowingFeedGenerator> logger, 
        FollowHelper followHelper,
        FeedRepository feedRepository)
    {
        _logger = logger;
        _followHelper = followHelper;
        _feedRepository = feedRepository;
    }
    
    public string GetUri(string handler) => $"at://{handler}/app.bsky.feed.generator/following-liked";

    public async Task<FeedResponse> RetrieveAsync(string? cursor, int limit,
        string issuerDid, CancellationToken cancellationToken)
    {
        var profiles = await _followHelper.GetFollowing(issuerDid, cancellationToken);
        var handles = profiles.Select(x => x.Did.Handler).ToHashSet();
        var results = await _feedRepository.GetLikesByHandlesAsync(handles, limit, cursor);
        return LikedFeedUtil.ConstructFeedResponse(results.LastOrDefault()?.Cursor ?? Cursor.Empty, results, profiles);
    }
}