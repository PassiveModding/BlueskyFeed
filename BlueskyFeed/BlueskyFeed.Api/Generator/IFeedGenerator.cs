namespace BlueskyFeed.Api.Generator;

public interface IFeedGenerator
{
    public string GetUri(string handler);
}