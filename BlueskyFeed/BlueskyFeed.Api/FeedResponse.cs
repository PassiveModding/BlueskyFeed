namespace BlueskyFeed.Api;

public record FeedResponseRecord(string Post, string FeedContext);

public record FeedResponse(string Cursor, FeedResponseRecord[] Feed)
{
    public object ToObject()
    {
        return new
        {
            cursor = Cursor,
            feed = Feed.Select(x => new
            {
                x.Post, x.FeedContext
            })
        };
    }
}