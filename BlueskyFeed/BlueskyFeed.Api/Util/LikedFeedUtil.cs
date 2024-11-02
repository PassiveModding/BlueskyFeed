using System.Text;
using BlueskyFeed.Common;
using FishyFlip.Models;

namespace BlueskyFeed.Api.Util;

public class LikedFeedUtil
{
    public static FeedResponse ConstructFeedResponse(Cursor newCursor, List<(Key Key, Like Like)> parsedResults, FeedProfile[] profiles)
    {
        var likedBuilder = new StringBuilder();
        return new FeedResponse(newCursor.ToString(), parsedResults
            .Where(x => x.Like.Subject?.Uri != null)
            .Select(x =>
            {
                var liked = profiles.Where(p => p.Did.Handler == x.Key.Handler).ToArray();
                likedBuilder.Clear();
                likedBuilder.Append("Liked by ");
                for (int i = 0; i < liked.Length; i++)
                {
                    var toAdd = i > 0 ? 
                        $", {liked[i].DisplayName} ({liked[i].Handle})" : 
                        $"{liked[i].DisplayName} ({liked[i].Handle})";
                    
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
                
                return new FeedResponseRecord(x.Like.Subject!.Uri!.ToString(), likedBuilder.ToString());
            })
            .ToArray());
    }
}