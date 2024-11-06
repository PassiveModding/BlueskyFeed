using System.Diagnostics;

namespace BlueskyFeed.Api;

public static class DiagnosticsConfig
{
    public const string SourceName = "BlueskyFeed.Api";
    public static ActivitySource Source { get; } = new(SourceName);
    
    public const string FeedKey = "feed";
    public const string CursorKey = "cursor";
    public const string IssuerDidKey = "issuer_did";
    public const string ScannedKey = "scanned";
    public const string MatchingKeysKey = "matching_keys";
    public const string LimitKey = "limit";
    public const string FollowingCountKey = "following_count";
    public const string FollowerCountKey = "follower_count";
    public const string IsCachedKey = "is_cached";
    
    public static Activity? WithFeed(this Activity? activity, string feed)
    {
        activity?.AddTag(FeedKey, feed);
        return activity;
    }
    
    public static Activity? WithCursor(this Activity? activity, string? cursor)
    {
        activity?.AddTag(CursorKey, cursor);
        return activity;
    }
    
    public static Activity? WithIssuerDid(this Activity? activity, string issuerDid)
    {
        activity?.AddTag(IssuerDidKey, issuerDid);
        return activity;
    }
    
    public static Activity? WithScanned(this Activity? activity, int scanned)
    {
        activity?.AddTag(ScannedKey, scanned);
        return activity;
    }
    
    public static Activity? WithMatchingKeys(this Activity? activity, int matchingKeys)
    {
        activity?.AddTag(MatchingKeysKey, matchingKeys);
        return activity;
    }
    
    public static Activity? WithLimit(this Activity? activity, int limit)
    {
        activity?.AddTag(LimitKey, limit);
        return activity;
    }
    
    public static Activity? WithFollowingCount(this Activity? activity, int followingCount)
    {
        activity?.AddTag(FollowingCountKey, followingCount);
        return activity;
    }
    
    public static Activity? WithFollowerCount(this Activity? activity, int followerCount)
    {
        activity?.AddTag(FollowerCountKey, followerCount);
        return activity;
    }
    
    public static Activity? WithIsCached(this Activity? activity, bool isCached)
    {
        activity?.AddTag(IsCachedKey, isCached);
        return activity;
    }
}