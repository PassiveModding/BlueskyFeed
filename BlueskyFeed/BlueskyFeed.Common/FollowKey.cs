namespace BlueskyFeed.Common;

public record FollowKey(string IssuerDid)
{
    public override string ToString() => $"follow::{IssuerDid}";
    public static FollowKey Parse(string key)
    {
        var parts = key.Split("::");
        return new FollowKey(parts[1]);
    }
}