using BlueskyFeed.Api.Services;
using BlueskyFeed.Api.Util;

namespace BlueskyFeed.Api.Generator;

public class TestFeedGenerator : IAuthorizedFeedGenerator
{
    private readonly ILogger<TestFeedGenerator> _logger;
    private readonly LikedByFollowingFeedGenerator _gen;

    public TestFeedGenerator(ILogger<TestFeedGenerator> logger, 
        LikedByFollowingFeedGenerator gen)
    {
        _logger = logger;
        _gen = gen;
    }
    
    public string GetUri(string handler) => $"at://{handler}/app.bsky.feed.generator/test-feed";

    public async Task<FeedResponse> RetrieveAsync(string? cursor, int limit,
        string issuerDid, CancellationToken cancellationToken)
    {
        return await _gen.RetrieveAsync(cursor, limit, issuerDid, cancellationToken);
    }
}