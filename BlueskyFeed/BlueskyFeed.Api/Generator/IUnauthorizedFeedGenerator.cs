namespace BlueskyFeed.Api.Generator;

public interface IUnauthorizedFeedGenerator : IFeedGenerator
{
    public Task<FeedResponse> RetrieveAsync(string? cursor, int limit, CancellationToken cancellationToken);
}