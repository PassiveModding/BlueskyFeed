using System.Text;
using BlueskyFeed.Common;
using FishyFlip.Models;
using LikeRecord = BlueskyFeed.Common.Db.LikeRecord;

namespace BlueskyFeed.Api.Util;

public static class LikedFeedUtil
{
    public static FeedResponse ConstructFeedResponse(Cursor newCursor, IEnumerable<LikeRecord> parsedResults, FeedProfile[] profiles)
    {
        var likedBuilder = new StringBuilder();
        return new FeedResponse(newCursor.ToString(), parsedResults
            .Select(x =>
            {
                var liked = profiles.Where(p => p.Did.Handler == x.GetDid()).ToArray();
                likedBuilder.Clear();
                likedBuilder.Append("Liked by ");
                for (int i = 0; i < liked.Length; i++)
                {
                    var relativeTime = x.IndexedAt.ToRelativeTime();
                    
                    var toAdd = i > 0 ? 
                        $", {liked[i].DisplayName} ({liked[i].Handle}) at {relativeTime}" : 
                        $"{liked[i].DisplayName} ({liked[i].Handle}) at {relativeTime}";
                    
                    if (likedBuilder.Length + toAdd.Length <= 2000)
                    {
                        likedBuilder.Append(toAdd);
                    }
                    else if (likedBuilder.Length + 3 <= 2000)
                    {
                        likedBuilder.Append("...");
                        break;
                    }
                }
                
                return new FeedResponseRecord(x.SubjectUri, likedBuilder.ToString());
            })
            .ToArray());
    }
    
    public static string ToRelativeTime(this DateTime time)
    {
        var timeSpan = DateTime.UtcNow - time;
        if (timeSpan.TotalDays > 365)
        {
            return $"{(int)(timeSpan.TotalDays / 365)} years ago";
        }
        if (timeSpan.TotalDays > 30)
        {
            return $"{(int)(timeSpan.TotalDays / 30)} months ago";
        }
        if (timeSpan.TotalDays > 7)
        {
            return $"{(int)(timeSpan.TotalDays / 7)} weeks ago";
        }
        if (timeSpan.TotalDays > 1)
        {
            return $"{(int)timeSpan.TotalDays} days ago";
        }
        if (timeSpan.TotalHours > 1)
        {
            return $"{(int)timeSpan.TotalHours} hours ago";
        }
        if (timeSpan.TotalMinutes > 1)
        {
            return $"{(int)timeSpan.TotalMinutes} minutes ago";
        }
        return "just now";
    }
}