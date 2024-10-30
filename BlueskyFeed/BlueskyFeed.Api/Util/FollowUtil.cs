using System.Text.Json;
using BlueskyFeed.Common;
using FishyFlip.Models;

namespace BlueskyFeed.Api.Util;

public static class FollowUtil
{
    public static string ToBlob(this FeedProfile profile)
    {
        return JsonSerializer.Serialize(profile, Entities.JsonSerializerOptions);
    }
    
    public static FeedProfile ParseFeedProfileBlob(string blob)
    {
        return JsonSerializer.Deserialize<FeedProfile>(blob, Entities.JsonSerializerOptions) ?? throw new Exception("Failed to deserialize FeedProfile");
    }
}