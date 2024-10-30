namespace BlueskyFeed.Api.Generator;

public interface IAuthorizedFeedGenerator : IFeedGenerator
{
    public Task<FeedResponse> RetrieveAsync(string? cursor, int limit, string issuerDid, CancellationToken cancellationToken);
}