namespace BlueskyFeed.Common;

public record Cursor(long Timestamp, string RKey)
{
    public override string ToString() => $"{Timestamp}:{RKey}";
    public static Cursor? FromString(string? cursor)
    {
        if (cursor == null) return null;
            
        var parts = cursor.Split(':');
            
        if (parts.Length != 2)
        {
            return null;
        }
            
        if (!long.TryParse(parts[0], out var timestamp))
        {
            return null;
        }
            
        return new Cursor(timestamp, parts[1]);
    }
    
    public static Cursor Empty => new(0, string.Empty);
}