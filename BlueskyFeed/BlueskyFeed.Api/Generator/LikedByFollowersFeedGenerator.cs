using BlueskyFeed.Api.Services;
using BlueskyFeed.Api.Util;
using BlueskyFeed.Common;
using BlueskyFeed.Common.Db;

namespace BlueskyFeed.Api.Generator;

public class LikedByFollowersFeedGenerator : IAuthorizedFeedGenerator
{
    private readonly ILogger<LikedByFollowersFeedGenerator> _logger;
    private readonly FollowHelper _followHelper;
    private readonly LikeRepository _likeRepository;

    public LikedByFollowersFeedGenerator(ILogger<LikedByFollowersFeedGenerator> logger, 
        FollowHelper followHelper,
        LikeRepository likeRepository)
    {
        _logger = logger;
        _followHelper = followHelper;
        _likeRepository = likeRepository;
    }
    
    public string GetUri(string handler) => $"at://{handler}/app.bsky.feed.generator/followers-liked";

    public async Task<FeedResponse> RetrieveAsync(string? cursor, int limit,
        string issuerDid, CancellationToken cancellationToken)
    {
        var profiles = await _followHelper.GetFollowers(issuerDid, cancellationToken);
        var handles = profiles.Select(x => x.Did).ToHashSet();
        var results = await _likeRepository.GetLikesByHandlesAsync(handles, limit, cursor);        
        var newCursor = results.LastOrDefault()?.Cursor ?? Cursor.Empty;
        if (newCursor.ToString() == cursor)
        {
            return new FeedResponse(Cursor.Empty.ToString(), []);
        }
        
        return LikedFeedUtil.ConstructFeedResponse(newCursor, results, profiles);
    }
}