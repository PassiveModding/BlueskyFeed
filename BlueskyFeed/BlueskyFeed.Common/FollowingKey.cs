namespace BlueskyFeed.Common;

public record FollowingKey(string IssuerDid)
{
    public override string ToString() => $"following::{IssuerDid}";
    public static FollowingKey Parse(string key)
    {
        var parts = key.Split("::");
        return new FollowingKey(parts[1]);
    }
}