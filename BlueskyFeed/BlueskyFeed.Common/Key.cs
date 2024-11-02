namespace BlueskyFeed.Common;

public record Key(string Collection, string Handler, string RKey)
{
    // handler contains : so we use :: as separator
    public override string ToString() => $"{Collection}::{Handler}::{RKey}";
    public static Key Parse(string key)
    {
        var parts = key.Split("::");
        return new Key(parts[0], parts[1], parts[2]);
    }
}